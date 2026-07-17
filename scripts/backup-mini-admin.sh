#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

APP_NAME="MiniAdmin Backup"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${APP_DIR}/.env"
BACKUP_ROOT="${MINIADMIN_BACKUP_DIR:-${APP_DIR}/backups}"
RETENTION_DAYS="${MINIADMIN_BACKUP_RETENTION_DAYS:-14}"
SKIP_UPLOADS="false"
API_RESTART_REQUIRED="false"
TEMP_DIR=""

usage() {
  cat <<'EOF'
MiniAdmin production backup

Usage:
  bash scripts/backup-mini-admin.sh [options]

Options:
  --output DIR          Backup root directory. Default: ./backups
  --retention-days N    Remove completed backups older than N days. Default: 14
  --skip-uploads        Back up MySQL only.
  -h, --help            Show help.
EOF
}

log() {
  printf '[%s] %s\n' "$APP_NAME" "$*"
}

fail() {
  printf '[%s] ERROR: %s\n' "$APP_NAME" "$*" >&2
  exit 1
}

compose() {
  docker compose --project-directory "$APP_DIR" --env-file "$ENV_FILE" "$@"
}

wait_for_container_health() {
  local container="$1"
  local timeout_seconds="$2"
  local started_at status
  started_at="$(date +%s)"
  while true; do
    status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container" 2>/dev/null || true)"
    if [[ "$status" == "healthy" || "$status" == "running" ]]; then
      return 0
    fi
    if (( $(date +%s) - started_at >= timeout_seconds )); then
      fail "Timed out waiting for ${container}; current status: ${status:-missing}."
    fi
    sleep 3
  done
}

restart_api_if_needed() {
  [[ "$API_RESTART_REQUIRED" == "true" ]] || return 0
  log "Restarting API after the consistent snapshot..."
  compose up -d api
  wait_for_container_health mini-admin-api 240
  API_RESTART_REQUIRED="false"
}

cleanup() {
  local exit_code="$?"
  if [[ -n "$TEMP_DIR" && -d "$TEMP_DIR" ]]; then
    rm -rf -- "$TEMP_DIR"
  fi
  if [[ "$API_RESTART_REQUIRED" == "true" ]]; then
    log "Backup did not finish; restarting API before exit."
    compose up -d api >/dev/null 2>&1 || true
  fi
  exit "$exit_code"
}

load_env_value() {
  local key="$1"
  local fallback="${2:-}"
  local value
  value="$(grep -E "^${key}=" "$ENV_FILE" 2>/dev/null | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%$'\r'}"
  value="${value#\"}"
  value="${value%\"}"
  [[ -n "$value" ]] && printf '%s' "$value" || printf '%s' "$fallback"
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --output)
        [[ $# -ge 2 ]] || fail "--output requires a directory."
        BACKUP_ROOT="$2"
        shift 2
        ;;
      --retention-days)
        [[ $# -ge 2 ]] || fail "--retention-days requires a number."
        RETENTION_DAYS="$2"
        shift 2
        ;;
      --skip-uploads)
        SKIP_UPLOADS="true"
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        fail "Unknown option: $1"
        ;;
    esac
  done
}

preflight() {
  [[ -f "$ENV_FILE" ]] || fail "Missing ${ENV_FILE}."
  command -v docker >/dev/null 2>&1 || fail "docker is required."
  command -v gzip >/dev/null 2>&1 || fail "gzip is required."
  command -v sha256sum >/dev/null 2>&1 || fail "sha256sum is required."
  docker compose version >/dev/null 2>&1 || fail "Docker Compose plugin is required."
  [[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] || fail "Retention days must be a non-negative integer."

  mkdir -p "$BACKUP_ROOT"
  BACKUP_ROOT="$(cd "$BACKUP_ROOT" && pwd -P)"
  [[ -n "$BACKUP_ROOT" && "$BACKUP_ROOT" != "/" ]] || fail "Unsafe backup root: ${BACKUP_ROOT}."
  chmod 700 "$BACKUP_ROOT" || true

  compose ps --status running --services | grep -qx mysql || fail "MySQL container is not running."
}

write_security_snapshot() {
  local output="$1"
  local key value
  : > "$output"
  for key in \
    MINIADMIN_JWT_SIGNING_KEY \
    MINIADMIN_OPEN_PLATFORM_SIGNING_KEY \
    MINIADMIN_OPEN_PLATFORM_ENCRYPTION_KEY \
    MINIADMIN_OPENAPI_CREDENTIAL_ENCRYPTION_KEY; do
    value="$(load_env_value "$key" "")"
    if [[ -n "$value" ]]; then
      printf '%s=%s\n' "$key" "$value" >> "$output"
    fi
  done
  chmod 600 "$output"
}

create_backup() {
  local timestamp final_dir uploads_volume helper_image git_commit
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  final_dir="${BACKUP_ROOT}/${timestamp}"
  TEMP_DIR="${BACKUP_ROOT}/.tmp-${timestamp}-$$"
  [[ ! -e "$final_dir" ]] || fail "Backup already exists: ${final_dir}."
  mkdir -p "$TEMP_DIR"

  if [[ "$SKIP_UPLOADS" != "true" ]] &&
     compose ps --status running --services | grep -qx api; then
    log "Stopping API briefly so the database and uploads snapshots stay consistent..."
    API_RESTART_REQUIRED="true"
    compose stop --timeout 75 api
  fi

  log "Dumping MySQL with a consistent transaction..."
  compose exec -T mysql sh -c \
    'exec mysqldump -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" --single-transaction --quick --routines --events --triggers --hex-blob --set-gtid-purged=OFF "$MYSQL_DATABASE"' \
    | gzip -9 > "${TEMP_DIR}/database.sql.gz"
  gzip -t "${TEMP_DIR}/database.sql.gz"

  if [[ "$SKIP_UPLOADS" != "true" ]]; then
    uploads_volume="$(load_env_value MINIADMIN_UPLOADS_VOLUME miniadmin_uploads)"
    helper_image="$(load_env_value MINIADMIN_NGINX_IMAGE nginx:1.27-alpine)"
    docker volume inspect "$uploads_volume" >/dev/null 2>&1 || fail "Uploads volume not found: ${uploads_volume}."
    log "Archiving uploads volume ${uploads_volume}..."
    docker run --rm --entrypoint sh \
      -v "${uploads_volume}:/data:ro" \
      -v "${TEMP_DIR}:/backup" \
      "$helper_image" \
      -c 'tar -C /data -czf /backup/uploads.tar.gz .'
  fi

  restart_api_if_needed

  cp "$ENV_FILE" "${TEMP_DIR}/environment.env"
  chmod 600 "${TEMP_DIR}/environment.env"
  write_security_snapshot "${TEMP_DIR}/security.env"
  git_commit="$(git -C "$APP_DIR" rev-parse HEAD 2>/dev/null || printf 'not-a-git-checkout')"
  cat > "${TEMP_DIR}/manifest.txt" <<EOF
format_version=1
created_at_utc=${timestamp}
application=MiniAdmin
git_commit=${git_commit}
mysql_database=$(load_env_value MINIADMIN_MYSQL_DATABASE mini_admin)
mysql_volume=$(load_env_value MINIADMIN_MYSQL_VOLUME miniadmin_mysql)
uploads_volume=$(load_env_value MINIADMIN_UPLOADS_VOLUME miniadmin_uploads)
uploads_included=$([[ "$SKIP_UPLOADS" == "true" ]] && printf 'false' || printf 'true')
EOF

  (
    cd "$TEMP_DIR"
    find . -maxdepth 1 -type f ! -name SHA256SUMS -printf '%f\n' \
      | LC_ALL=C sort \
      | xargs -r sha256sum > SHA256SUMS
    sha256sum -c SHA256SUMS >/dev/null
  )

  mv "$TEMP_DIR" "$final_dir"
  TEMP_DIR=""
  log "Backup completed: ${final_dir}"
  log "Keep this directory private; environment.env and security.env contain secrets."
}

cleanup_old_backups() {
  if (( RETENTION_DAYS == 0 )); then
    return
  fi

  find "$BACKUP_ROOT" \
    -mindepth 1 -maxdepth 1 -type d \
    -name '20??????T??????Z' \
    -mtime "+${RETENTION_DAYS}" \
    -exec rm -rf -- {} +
}

main() {
  trap cleanup EXIT
  parse_args "$@"
  preflight
  create_backup
  cleanup_old_backups
}

main "$@"
