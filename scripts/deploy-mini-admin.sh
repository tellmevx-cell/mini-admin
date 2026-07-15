#!/usr/bin/env bash
set -Eeuo pipefail

APP_NAME="MiniAdmin"
DEFAULT_WEB_PORT="5666"
DEFAULT_API_BIND="127.0.0.1:8080"
DEFAULT_GATEWAY_BIND="127.0.0.1:8088"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${APP_DIR}/.env"
FORCE_ENV="false"
RUN_GIT_PULL="false"
SKIP_BUILD="false"
SHOW_LOGS="false"

usage() {
  cat <<'EOF'
MiniAdmin one-click Docker Compose deploy script.

Usage:
  bash scripts/deploy-mini-admin.sh [options]

Options:
  --force-env       Backup and regenerate .env.
  --pull            Run git pull before deploying.
  --skip-build      Start containers without rebuilding images.
  --logs            Show docker compose logs after deployment.
  -h, --help        Show this help message.

Environment overrides:
  MINIADMIN_WEB_PORT=5666
  MINIADMIN_HTTP_PORT=127.0.0.1:8080
  MINIADMIN_GATEWAY_PORT=127.0.0.1:8088

Examples:
  bash scripts/deploy-mini-admin.sh
  bash scripts/deploy-mini-admin.sh --pull
  MINIADMIN_WEB_PORT=8090 bash scripts/deploy-mini-admin.sh
EOF
}

log() {
  printf '\033[1;34m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

warn() {
  printf '\033[1;33m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

fail() {
  printf '\033[1;31m[%s]\033[0m %s\n' "$APP_NAME" "$*" >&2
  exit 1
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --force-env)
        FORCE_ENV="true"
        shift
        ;;
      --pull)
        RUN_GIT_PULL="true"
        shift
        ;;
      --skip-build)
        SKIP_BUILD="true"
        shift
        ;;
      --logs)
        SHOW_LOGS="true"
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

need_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing command: $1"
}

random_hex() {
  local length="$1"
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex "$length"
  else
    tr -dc 'A-Za-z0-9' </dev/urandom | head -c "$((length * 2))"
  fi
}

compose() {
  docker compose "$@"
}

parse_port() {
  local value="$1"
  printf '%s' "${value##*:}"
}

load_env_value() {
  local key="$1"
  local default_value="$2"
  local value
  value="$(grep -E "^${key}=" "$ENV_FILE" 2>/dev/null | tail -n 1 | cut -d '=' -f 2- || true)"
  if [[ -z "$value" ]]; then
    printf '%s' "$default_value"
  else
    printf '%s' "$value"
  fi
}

has_placeholder_env() {
  [[ -f "$ENV_FILE" ]] && grep -Eq 'change_me|replace_' "$ENV_FILE"
}

write_env_file() {
  local web_port="${MINIADMIN_WEB_PORT:-$DEFAULT_WEB_PORT}"
  local api_bind="${MINIADMIN_HTTP_PORT:-$DEFAULT_API_BIND}"
  local gateway_bind="${MINIADMIN_GATEWAY_PORT:-$DEFAULT_GATEWAY_BIND}"
  local jwt_key mysql_password mysql_root_password redis_password

  jwt_key="$(random_hex 40)"
  mysql_password="$(random_hex 24)"
  mysql_root_password="$(random_hex 24)"
  redis_password="$(random_hex 24)"

  cat > "$ENV_FILE" <<EOF
MINIADMIN_HTTP_PORT=${api_bind}
MINIADMIN_GATEWAY_PORT=${gateway_bind}
MINIADMIN_WEB_PORT=${web_port}

MINIADMIN_GATEWAY_RATE_LIMITING_ENABLED=${MINIADMIN_GATEWAY_RATE_LIMITING_ENABLED:-true}
MINIADMIN_GATEWAY_RATE_LIMITING_PERMIT_LIMIT=${MINIADMIN_GATEWAY_RATE_LIMITING_PERMIT_LIMIT:-1200}
MINIADMIN_GATEWAY_RATE_LIMITING_WINDOW_SECONDS=${MINIADMIN_GATEWAY_RATE_LIMITING_WINDOW_SECONDS:-60}
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_PERMIT_LIMIT=${MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_PERMIT_LIMIT:-20}
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_WINDOW_SECONDS=${MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_WINDOW_SECONDS:-60}

MINIADMIN_NPM_REGISTRY=${MINIADMIN_NPM_REGISTRY:-https://registry.npmmirror.com}
MINIADMIN_PNPM_FETCH_TIMEOUT=${MINIADMIN_PNPM_FETCH_TIMEOUT:-600000}
MINIADMIN_PNPM_FETCH_RETRIES=${MINIADMIN_PNPM_FETCH_RETRIES:-5}
MINIADMIN_PNPM_NETWORK_CONCURRENCY=${MINIADMIN_PNPM_NETWORK_CONCURRENCY:-4}

MINIADMIN_JWT_SIGNING_KEY=${jwt_key}

MINIADMIN_MYSQL_DATABASE=mini_admin
MINIADMIN_MYSQL_USER=miniadmin
MINIADMIN_MYSQL_PASSWORD=${mysql_password}
MINIADMIN_MYSQL_ROOT_PASSWORD=${mysql_root_password}

MINIADMIN_REDIS_PASSWORD=${redis_password}

MINIADMIN_UPLOADS_VOLUME=miniadmin_uploads
MINIADMIN_MYSQL_VOLUME=miniadmin_mysql
MINIADMIN_REDIS_VOLUME=miniadmin_redis
EOF
  chmod 600 "$ENV_FILE" || true
}

ensure_env_file() {
  if [[ "$FORCE_ENV" == "true" && -f "$ENV_FILE" ]]; then
    local backup="${ENV_FILE}.bak.$(date +%Y%m%d%H%M%S)"
    cp "$ENV_FILE" "$backup"
    warn "Existing .env backed up to ${backup}"
    write_env_file
    log ".env regenerated."
    return
  fi

  if has_placeholder_env; then
    local backup="${ENV_FILE}.bak.$(date +%Y%m%d%H%M%S)"
    cp "$ENV_FILE" "$backup"
    warn "Placeholder .env backed up to ${backup}"
    write_env_file
    log ".env regenerated with random secrets."
    return
  fi

  if [[ ! -f "$ENV_FILE" ]]; then
    write_env_file
    log ".env created with random secrets."
  else
    log "Using existing .env. It will not be overwritten."
  fi
}

wait_for_http() {
  local url="$1"
  local label="$2"
  local max_attempts="${3:-60}"

  if ! command -v curl >/dev/null 2>&1; then
    warn "curl is not installed. Skip HTTP health check for ${label}."
    return 0
  fi

  for attempt in $(seq 1 "$max_attempts"); do
    if curl -fsS "$url" >/dev/null 2>&1; then
      log "${label} is ready: ${url}"
      return 0
    fi
    sleep 3
  done

  warn "${label} did not pass HTTP check in time: ${url}"
  return 1
}

detect_server_ip() {
  local ip
  ip="$(hostname -I 2>/dev/null | awk '{print $1}' || true)"
  if [[ -z "$ip" ]]; then
    ip="SERVER_IP"
  fi
  printf '%s' "$ip"
}

preflight() {
  [[ -f "${APP_DIR}/docker-compose.yml" ]] || fail "docker-compose.yml not found. Run this script inside the mini-admin repository."
  need_command docker
  docker compose version >/dev/null 2>&1 || fail "Docker Compose plugin is not available. Install Docker Compose or enable it in 1Panel."

  if [[ "$RUN_GIT_PULL" == "true" ]]; then
    if [[ -d "${APP_DIR}/.git" ]]; then
      need_command git
      log "Pulling latest code..."
      git -C "$APP_DIR" pull --ff-only
    else
      warn "--pull ignored because this directory is not a git repository."
    fi
  fi
}

deploy() {
  cd "$APP_DIR"
  ensure_env_file

  log "Validating Docker Compose configuration..."
  compose config >/dev/null

  if [[ "$SKIP_BUILD" == "true" ]]; then
    log "Starting containers without rebuild..."
    compose up -d --remove-orphans
  else
    log "Building and starting containers..."
    compose up -d --build --remove-orphans
  fi

  log "Current containers:"
  compose ps

  local api_bind gateway_bind web_bind api_port gateway_port web_port server_ip
  api_bind="$(load_env_value MINIADMIN_HTTP_PORT "$DEFAULT_API_BIND")"
  gateway_bind="$(load_env_value MINIADMIN_GATEWAY_PORT "$DEFAULT_GATEWAY_BIND")"
  web_bind="$(load_env_value MINIADMIN_WEB_PORT "$DEFAULT_WEB_PORT")"
  api_port="$(parse_port "$api_bind")"
  gateway_port="$(parse_port "$gateway_bind")"
  web_port="$(parse_port "$web_bind")"
  server_ip="$(detect_server_ip)"

  wait_for_http "http://127.0.0.1:${api_port}/health" "API health" 80 || true
  wait_for_http "http://127.0.0.1:${gateway_port}/health" "Gateway health" 80 || true
  wait_for_http "http://127.0.0.1:${web_port}" "Web" 80 || true

  cat <<EOF

Deployment finished.

Visit:
  Web:            http://${server_ip}:${web_port}
  Gateway health: http://127.0.0.1:${gateway_port}/health
  API health:     http://127.0.0.1:${api_port}/health

Default accounts:
  Platform admin: tenant blank, username admin, password 123456
  Demo tenant:    tenant demo,  username demo,  password 123456

Important:
  Change default passwords immediately after first login.
  Keep ${ENV_FILE} private. It contains production secrets.

Useful commands:
  cd ${APP_DIR}
  docker compose ps
  docker compose logs -f gateway
  docker compose logs -f api
  docker compose logs -f web
  docker compose down
EOF

  if [[ "$SHOW_LOGS" == "true" ]]; then
    compose logs -f
  fi
}

parse_args "$@"
preflight
deploy
