#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

APP_NAME="MiniAdmin Restore"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${APP_DIR}/.env"
BACKUP_DIR=""
CONFIRMED="false"
SKIP_SAFETY_BACKUP="false"
SKIP_UPLOADS="false"
KEEP_CURRENT_SECURITY="false"
SERVICES_STOPPED="false"

usage() {
  cat <<'EOF'
MiniAdmin verified restore

Usage:
  bash scripts/restore-mini-admin.sh BACKUP_DIR --confirm [options]

Options:
  --confirm                 Required destructive-operation confirmation.
  --skip-safety-backup      Do not create a backup of the current system first.
  --skip-uploads            Restore MySQL only.
  --keep-current-security   Do not restore JWT/OpenPlatform encryption keys.
  -h, --help                Show help.
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
  [[ $# -gt 0 ]] || { usage; exit 1; }
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --confirm)
        CONFIRMED="true"
        shift
        ;;
      --skip-safety-backup)
        SKIP_SAFETY_BACKUP="true"
        shift
        ;;
      --skip-uploads)
        SKIP_UPLOADS="true"
        shift
        ;;
      --keep-current-security)
        KEEP_CURRENT_SECURITY="true"
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      --*)
        fail "Unknown option: $1"
        ;;
      *)
        [[ -z "$BACKUP_DIR" ]] || fail "Only one backup directory may be supplied."
        BACKUP_DIR="$1"
        shift
        ;;
    esac
  done
}

preflight() {
  [[ "$CONFIRMED" == "true" ]] || fail "Restore is destructive. Re-run with --confirm."
  [[ -f "$ENV_FILE" ]] || fail "Missing ${ENV_FILE}."
  [[ -n "$BACKUP_DIR" && -d "$BACKUP_DIR" ]] || fail "Backup directory not found: ${BACKUP_DIR}."
  BACKUP_DIR="$(cd "$BACKUP_DIR" && pwd -P)"
  [[ -f "${BACKUP_DIR}/database.sql.gz" ]] || fail "database.sql.gz is missing."
  [[ -f "${BACKUP_DIR}/SHA256SUMS" ]] || fail "SHA256SUMS is missing."
  [[ -f "${BACKUP_DIR}/manifest.txt" ]] || fail "manifest.txt is missing."
  command -v docker >/dev/null 2>&1 || fail "docker is required."
  command -v gzip >/dev/null 2>&1 || fail "gzip is required."
  command -v sha256sum >/dev/null 2>&1 || fail "sha256sum is required."
  docker compose version >/dev/null 2>&1 || fail "Docker Compose plugin is required."

  log "Verifying backup checksums..."
  (cd "$BACKUP_DIR" && sha256sum -c SHA256SUMS)
  gzip -t "${BACKUP_DIR}/database.sql.gz"
  grep -qx 'format_version=1' "${BACKUP_DIR}/manifest.txt" || fail "Unsupported backup format."

  log "Ensuring MySQL and Redis are available for restore..."
  compose up -d mysql redis
  wait_for_health mini-admin-mysql 180
  wait_for_health mini-admin-redis 90
}

restore_security_keys() {
  [[ "$KEEP_CURRENT_SECURITY" != "true" ]] || return 0
  [[ -f "${BACKUP_DIR}/security.env" ]] || {
    log "security.env is absent; current security keys will be kept."
    return 0
  }

  cp "$ENV_FILE" "${ENV_FILE}.pre-restore.$(date -u +%Y%m%dT%H%M%SZ)"
  local line key value temp_file
  temp_file="${ENV_FILE}.tmp.$$"
  cp "$ENV_FILE" "$temp_file"
  while IFS= read -r line || [[ -n "$line" ]]; do
    [[ "$line" == *=* ]] || continue
    key="${line%%=*}"
    value="${line#*=}"
    case "$key" in
      MINIADMIN_JWT_SIGNING_KEY|MINIADMIN_OPEN_PLATFORM_SIGNING_KEY|MINIADMIN_OPEN_PLATFORM_ENCRYPTION_KEY|MINIADMIN_OPENAPI_CREDENTIAL_ENCRYPTION_KEY)
        if grep -qE "^${key}=" "$temp_file"; then
          awk -v key="$key" -v value="$value" \
            'BEGIN { FS="=" } $1 == key { print key "=" value; next } { print }' \
            "$temp_file" > "${temp_file}.next"
          mv "${temp_file}.next" "$temp_file"
        else
          printf '\n%s=%s\n' "$key" "$value" >> "$temp_file"
        fi
        ;;
    esac
  done < "${BACKUP_DIR}/security.env"
  mv "$temp_file" "$ENV_FILE"
  chmod 600 "$ENV_FILE" || true
}

restart_on_error() {
  local exit_code="$?"
  if [[ "$SERVICES_STOPPED" == "true" ]]; then
    log "Restore failed; restarting services for manual inspection."
    compose up -d mysql redis api gateway web || true
  fi
  exit "$exit_code"
}

wait_for_health() {
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

restore_data() {
  local database_name uploads_volume helper_image
  local -a safety_backup_args
  database_name="$(load_env_value MINIADMIN_MYSQL_DATABASE mini_admin)"
  [[ "$database_name" =~ ^[A-Za-z0-9_]+$ ]] || fail "Unsafe database name: ${database_name}."

  if [[ "$SKIP_SAFETY_BACKUP" != "true" ]]; then
    log "Creating pre-restore safety backup..."
    safety_backup_args=(
      --output "${APP_DIR}/backups/pre-restore"
      --retention-days 30
    )
    uploads_volume="$(load_env_value MINIADMIN_UPLOADS_VOLUME miniadmin_uploads)"
    if ! docker volume inspect "$uploads_volume" >/dev/null 2>&1; then
      log "Current uploads volume does not exist; safety backup will include MySQL only."
      safety_backup_args+=(--skip-uploads)
    fi
    bash "${SCRIPT_DIR}/backup-mini-admin.sh" "${safety_backup_args[@]}"
  fi

  log "Stopping write services..."
  compose stop web gateway api
  SERVICES_STOPPED="true"
  trap restart_on_error ERR

  log "Recreating database ${database_name}..."
  compose exec -T mysql sh -c \
    'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "DROP DATABASE IF EXISTS \`$MYSQL_DATABASE\`; CREATE DATABASE \`$MYSQL_DATABASE\` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"'
  gzip -dc "${BACKUP_DIR}/database.sql.gz" \
    | compose exec -T mysql sh -c 'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE"'

  if [[ "$SKIP_UPLOADS" != "true" && -f "${BACKUP_DIR}/uploads.tar.gz" ]]; then
    uploads_volume="$(load_env_value MINIADMIN_UPLOADS_VOLUME miniadmin_uploads)"
    helper_image="$(load_env_value MINIADMIN_NGINX_IMAGE nginx:1.27-alpine)"
    log "Restoring uploads volume ${uploads_volume}..."
    docker run --rm --entrypoint sh \
      -v "${uploads_volume}:/data" \
      -v "${BACKUP_DIR}:/backup:ro" \
      "$helper_image" \
      -c 'rm -rf /data/* /data/.[!.]* /data/..?*; tar -C /data -xzf /backup/uploads.tar.gz'
  fi

  restore_security_keys

  log "Clearing Redis cache so restored database state cannot use stale authorization snapshots..."
  compose exec -T redis sh -c 'redis-cli -a "$REDIS_PASSWORD" FLUSHDB >/dev/null'

  log "Starting MiniAdmin..."
  compose up -d mysql redis api gateway web
  wait_for_health mini-admin-api 240
  wait_for_health mini-admin-gateway 90
  wait_for_health mini-admin-web 90
  SERVICES_STOPPED="false"
  trap - ERR
  log "Restore completed and all services are healthy."
}

main() {
  parse_args "$@"
  preflight
  restore_data
}

main "$@"
