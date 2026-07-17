#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

APP_NAME="MiniAdmin Acceptance"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${APP_DIR}/.env"
WEB_URL=""
CHECK_OPENAPI="false"
WITH_LOGIN="false"
WITH_BACKUP="false"
ALLOW_INSECURE_TLS="false"
PASSED_CHECKS=0
WARNINGS=0
TEMP_DIR=""

usage() {
  cat <<'EOF'
MiniAdmin production acceptance

Usage:
  bash scripts/acceptance-mini-admin.sh [options]

Options:
  --env-file FILE      Compose environment file. Default: ./.env
  --web-url URL        Public or local Web URL. Defaults to MINIADMIN_WEB_PORT.
  --check-openapi      Require /api/openapi/v1.json to be available.
  --with-login         Verify login and /user/info with acceptance credentials.
  --with-backup        Create a verified production backup after read-only checks.
  --insecure           Skip TLS certificate verification for a staging URL only.
  -h, --help           Show help.

Authenticated smoke-test credentials are read from environment variables so the
password does not need to be written to shell history:

  MINIADMIN_ACCEPTANCE_USERNAME=admin
  MINIADMIN_ACCEPTANCE_PASSWORD='your-password'
  MINIADMIN_ACCEPTANCE_TENANT_CODE=''

Examples:
  bash scripts/acceptance-mini-admin.sh
  bash scripts/acceptance-mini-admin.sh --web-url https://admin.example.com
  MINIADMIN_ACCEPTANCE_USERNAME=admin \
  MINIADMIN_ACCEPTANCE_PASSWORD='your-password' \
    bash scripts/acceptance-mini-admin.sh --with-login --with-backup
EOF
}

log() {
  printf '\033[1;34m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

pass() {
  PASSED_CHECKS=$((PASSED_CHECKS + 1))
  printf '\033[1;32m[%s]\033[0m PASS  %s\n' "$APP_NAME" "$*"
}

warn() {
  WARNINGS=$((WARNINGS + 1))
  printf '\033[1;33m[%s]\033[0m WARN  %s\n' "$APP_NAME" "$*"
}

fail() {
  printf '\033[1;31m[%s]\033[0m FAIL  %s\n' "$APP_NAME" "$*" >&2
  exit 1
}

cleanup() {
  local exit_code="$?"
  if [[ -n "$TEMP_DIR" && -d "$TEMP_DIR" ]]; then
    rm -rf -- "$TEMP_DIR"
  fi
  exit "$exit_code"
}

trap cleanup EXIT

compose() {
  docker compose \
    --project-directory "$APP_DIR" \
    --env-file "$ENV_FILE" \
    "$@"
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing required command: $1"
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

binding_port() {
  printf '%s' "${1##*:}"
}

binding_check_host() {
  local binding="$1"
  local host="127.0.0.1"
  if [[ "$binding" == *:* ]]; then
    host="${binding%:*}"
  fi
  case "$host" in
    ""|"0.0.0.0"|"::"|"[::]") host="127.0.0.1" ;;
  esac
  printf '%s' "$host"
}

is_loopback_binding() {
  local binding="$1"
  [[ "$binding" != *:* ]] && return 1
  case "${binding%:*}" in
    "127.0.0.1"|"localhost"|"::1"|"[::1]") return 0 ;;
    *) return 1 ;;
  esac
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --env-file)
        [[ $# -ge 2 ]] || fail "--env-file requires a file."
        ENV_FILE="$2"
        shift 2
        ;;
      --web-url)
        [[ $# -ge 2 ]] || fail "--web-url requires a URL."
        WEB_URL="${2%/}"
        shift 2
        ;;
      --check-openapi)
        CHECK_OPENAPI="true"
        shift
        ;;
      --with-login)
        WITH_LOGIN="true"
        shift
        ;;
      --with-backup)
        WITH_BACKUP="true"
        shift
        ;;
      --insecure)
        ALLOW_INSECURE_TLS="true"
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

validate_secret() {
  local key="$1"
  local value
  value="$(load_env_value "$key")"
  [[ -n "$value" ]] || fail "${key} is missing from ${ENV_FILE}."
  [[ ! "$value" =~ change_me|replace_with|replace_mysql|replace_redis ]] || fail "${key} still contains a placeholder."
  (( ${#value} >= 32 )) || fail "${key} must contain at least 32 characters."
}

validate_environment() {
  local jwt_key signing_key encryption_key credential_key issuer allow_insecure
  local api_binding gateway_binding env_mode

  [[ -f "$ENV_FILE" ]] || fail "Environment file not found: ${ENV_FILE}"
  ENV_FILE="$(cd "$(dirname "$ENV_FILE")" && pwd -P)/$(basename "$ENV_FILE")"

  validate_secret MINIADMIN_JWT_SIGNING_KEY
  validate_secret MINIADMIN_OPEN_PLATFORM_SIGNING_KEY
  validate_secret MINIADMIN_OPEN_PLATFORM_ENCRYPTION_KEY
  validate_secret MINIADMIN_OPENAPI_CREDENTIAL_ENCRYPTION_KEY
  validate_secret MINIADMIN_MYSQL_PASSWORD
  validate_secret MINIADMIN_MYSQL_ROOT_PASSWORD
  validate_secret MINIADMIN_REDIS_PASSWORD

  jwt_key="$(load_env_value MINIADMIN_JWT_SIGNING_KEY)"
  signing_key="$(load_env_value MINIADMIN_OPEN_PLATFORM_SIGNING_KEY)"
  encryption_key="$(load_env_value MINIADMIN_OPEN_PLATFORM_ENCRYPTION_KEY)"
  credential_key="$(load_env_value MINIADMIN_OPENAPI_CREDENTIAL_ENCRYPTION_KEY)"
  pass "Production secrets exist and contain no placeholders."
  if [[ "$jwt_key" == "$signing_key" ||
        "$jwt_key" == "$encryption_key" ||
        "$jwt_key" == "$credential_key" ||
        "$signing_key" == "$encryption_key" ||
        "$signing_key" == "$credential_key" ||
        "$encryption_key" == "$credential_key" ]]; then
    warn "One or more security keys are reused. Keep compatibility for now, then follow the documented maintenance-window rotation procedure."
  else
    pass "JWT and OpenPlatform keys are independently generated."
  fi

  issuer="$(load_env_value MINIADMIN_OPEN_PLATFORM_ISSUER)"
  allow_insecure="$(load_env_value MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP false)"
  [[ "$issuer" =~ ^https?:// ]] || fail "MINIADMIN_OPEN_PLATFORM_ISSUER must be an absolute HTTP(S) URL."
  [[ "$issuer" == */ ]] || fail "MINIADMIN_OPEN_PLATFORM_ISSUER must end with '/'."
  if [[ "$issuer" == http://* ]]; then
    [[ "${allow_insecure,,}" == "true" ]] || fail "HTTP issuer requires MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=true."
    warn "OIDC issuer uses HTTP. Use HTTPS before exposing this environment publicly."
  elif [[ "${allow_insecure,,}" == "true" ]]; then
    warn "OIDC issuer uses HTTPS but insecure HTTP compatibility remains enabled."
  else
    pass "OIDC issuer requires HTTPS."
  fi

  api_binding="$(load_env_value MINIADMIN_HTTP_PORT 127.0.0.1:8080)"
  gateway_binding="$(load_env_value MINIADMIN_GATEWAY_PORT 127.0.0.1:8088)"
  is_loopback_binding "$api_binding" \
    && pass "API port is bound to loopback only (${api_binding})." \
    || warn "API port is not loopback-only (${api_binding}); normally only Web should be public."
  is_loopback_binding "$gateway_binding" \
    && pass "Gateway port is bound to loopback only (${gateway_binding})." \
    || warn "Gateway port is not loopback-only (${gateway_binding}); normally only Web should be public."

  if command -v stat >/dev/null 2>&1; then
    env_mode="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || true)"
    case "$env_mode" in
      400|600) pass "Environment file permissions are restricted (${env_mode})." ;;
      *) warn "Restrict ${ENV_FILE} to its owner (recommended: chmod 600); current mode=${env_mode:-unknown}." ;;
    esac
  fi
}

verify_container() {
  local service="$1"
  local container_id state health restart_count
  container_id="$(compose ps -q "$service" 2>/dev/null || true)"
  [[ -n "$container_id" ]] || fail "Service ${service} has no container."
  state="$(docker inspect --format '{{.State.Status}}' "$container_id" 2>/dev/null || true)"
  health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id" 2>/dev/null || true)"
  restart_count="$(docker inspect --format '{{.RestartCount}}' "$container_id" 2>/dev/null || printf 'unknown')"
  [[ "$state" == "running" ]] || fail "Service ${service} is ${state:-missing}."
  [[ "$health" == "healthy" ]] || fail "Service ${service} health is ${health}."
  pass "Container ${service} is healthy (restart count: ${restart_count})."
}

curl_request() {
  local url="$1"
  local body_file="$2"
  local header_file="$3"
  shift 3
  local -a args=(
    --fail
    --silent
    --show-error
    --location
    --connect-timeout 8
    --max-time 30
    --dump-header "$header_file"
    --output "$body_file"
  )
  [[ "$ALLOW_INSECURE_TLS" == "true" ]] && args+=(--insecure)
  curl "${args[@]}" "$@" "$url"
}

verify_http() {
  local url="$1"
  local label="$2"
  local required_text="${3:-}"
  local body_file="${TEMP_DIR}/body-${PASSED_CHECKS}.txt"
  local header_file="${TEMP_DIR}/headers-${PASSED_CHECKS}.txt"
  local -a request_args=()
  if (( $# > 3 )); then
    request_args=("${@:4}")
  fi
  curl_request "$url" "$body_file" "$header_file" "${request_args[@]}"
  if [[ -n "$required_text" ]]; then
    grep -Fq "$required_text" "$body_file" || fail "${label} returned an unexpected response; missing '${required_text}'."
  fi
  pass "${label}: ${url}"
}

verify_gateway_trace() {
  local gateway_binding gateway_host gateway_port body_file header_file url
  gateway_binding="$(load_env_value MINIADMIN_GATEWAY_PORT 127.0.0.1:8088)"
  gateway_host="$(binding_check_host "$gateway_binding")"
  gateway_port="$(binding_port "$gateway_binding")"
  url="http://${gateway_host}:${gateway_port}/api/health/ready"
  body_file="${TEMP_DIR}/gateway-body.txt"
  header_file="${TEMP_DIR}/gateway-headers.txt"
  curl_request "$url" "$body_file" "$header_file"
  grep -Fq 'primary-cache' "$body_file" || fail "Gateway-to-API readiness response is incomplete."
  grep -Eqi '^X-Trace-Id:' "$header_file" || fail "Gateway response is missing X-Trace-Id."
  pass "Gateway routing and trace propagation are healthy."
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  value="${value//$'\r'/\\r}"
  value="${value//$'\t'/\\t}"
  printf '%s' "$value"
}

verify_login() {
  local username="${MINIADMIN_ACCEPTANCE_USERNAME:-}"
  local password="${MINIADMIN_ACCEPTANCE_PASSWORD:-}"
  local tenant_code="${MINIADMIN_ACCEPTANCE_TENANT_CODE:-}"
  local payload_file response_file header_file token tenant_json

  [[ -n "$username" ]] || fail "MINIADMIN_ACCEPTANCE_USERNAME is required with --with-login."
  [[ -n "$password" ]] || fail "MINIADMIN_ACCEPTANCE_PASSWORD is required with --with-login."
  [[ "$password" != "123456" ]] || warn "Acceptance login still uses the default password; change it before release."

  payload_file="${TEMP_DIR}/login.json"
  response_file="${TEMP_DIR}/login-response.json"
  header_file="${TEMP_DIR}/login-headers.txt"
  tenant_json=""
  if [[ -n "$tenant_code" ]]; then
    tenant_json=",\"tenantCode\":\"$(json_escape "$tenant_code")\""
  fi
  printf '{"username":"%s","password":"%s"%s}\n' \
    "$(json_escape "$username")" \
    "$(json_escape "$password")" \
    "$tenant_json" > "$payload_file"

  curl_request \
    "${WEB_URL}/api/auth/login" \
    "$response_file" \
    "$header_file" \
    --header 'Content-Type: application/json' \
    --data-binary "@${payload_file}"
  token="$(sed -n 's/.*"accessToken"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$response_file" | head -n 1)"
  [[ -n "$token" ]] || fail "Login succeeded at HTTP level but no access token was returned."
  pass "Authenticated login smoke test succeeded for ${username}."

  verify_http \
    "${WEB_URL}/api/user/info" \
    "Authenticated current-user query" \
    "$username" \
    --header "Authorization: Bearer ${token}"

  curl_request \
    "${WEB_URL}/api/auth/logout" \
    "${TEMP_DIR}/logout-response.json" \
    "${TEMP_DIR}/logout-headers.txt" \
    --request POST \
    --header "Authorization: Bearer ${token}"
  pass "Acceptance session was signed out."
}

verify_stack() {
  local web_binding web_host web_port
  web_binding="$(load_env_value MINIADMIN_WEB_PORT 5666)"
  web_host="$(binding_check_host "$web_binding")"
  web_port="$(binding_port "$web_binding")"
  [[ -n "$WEB_URL" ]] || WEB_URL="http://${web_host}:${web_port}"
  WEB_URL="${WEB_URL%/}"

  log "Validating Compose deployment contract..."
  compose config --quiet
  pass "Docker Compose configuration is valid."

  for service in mysql redis api gateway web; do
    verify_container "$service"
  done

  verify_http "${WEB_URL}/" "Web application"
  verify_http "${WEB_URL}/api/health/live" "API liveness through Web and Gateway" 'self'
  verify_http "${WEB_URL}/api/health/ready" "API readiness through Web and Gateway" 'primary-cache'
  verify_http "${WEB_URL}/.well-known/openid-configuration" "OIDC discovery" 'issuer'
  verify_gateway_trace

  if [[ "$CHECK_OPENAPI" == "true" ]]; then
    verify_http "${WEB_URL}/api/openapi/v1.json" "OpenAPI document" 'openapi'
  fi

  if [[ "$WITH_LOGIN" == "true" ]]; then
    verify_login
  fi

  if [[ "$WITH_BACKUP" == "true" ]]; then
    [[ "$ENV_FILE" == "${APP_DIR}/.env" ]] \
      || fail "--with-backup currently requires the deployment environment file at ${APP_DIR}/.env."
    log "Creating a production backup..."
    bash "${SCRIPT_DIR}/backup-mini-admin.sh"
    pass "Production backup completed."
  fi
}

main() {
  parse_args "$@"
  need_command docker
  need_command curl
  need_command grep
  need_command sed
  docker compose version >/dev/null 2>&1 || fail "Docker Compose plugin is required."
  docker info >/dev/null 2>&1 || fail "Docker daemon is not running."
  TEMP_DIR="$(mktemp -d)"

  validate_environment
  verify_stack

  printf '\n'
  log "Acceptance passed: ${PASSED_CHECKS} checks, ${WARNINGS} warning(s)."
  log "Restore remains a separate destructive drill and must be run in an isolated environment."
}

main "$@"
