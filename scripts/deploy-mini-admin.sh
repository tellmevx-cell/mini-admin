#!/usr/bin/env bash
set -Eeuo pipefail

APP_NAME="MiniAdmin"
DEFAULT_WEB_PORT="5666"
DEFAULT_API_BIND="127.0.0.1:8080"
DEFAULT_GATEWAY_BIND="127.0.0.1:8088"
DEFAULT_BUILD_RETRIES="2"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${APP_DIR}/.env"

FORCE_ENV="false"
RUN_GIT_PULL="false"
SKIP_BUILD="false"
NO_CACHE="false"
SHOW_LOGS="false"
DEPLOY_ACTIVE="false"
CURRENT_STAGE="启动前检查"
CURRENT_SERVICE=""

usage() {
  cat <<'EOF'
MiniAdmin Docker Compose 一键部署脚本

用法：
  bash deploy.sh [选项]
  bash scripts/deploy-mini-admin.sh [选项]

选项：
  --force-env       备份并重新生成 .env，仅适用于没有 MySQL 数据卷的环境。
  --pull            部署前执行 git pull --ff-only。
  --skip-build      不构建镜像，直接使用服务器上的现有镜像。
  --no-cache        不使用 Docker 镜像层缓存重新构建。
  --logs            部署成功后持续显示全部容器日志。
  -h, --help        显示帮助。

环境变量覆盖：
  MINIADMIN_WEB_PORT=5666
  MINIADMIN_HTTP_PORT=127.0.0.1:8080
  MINIADMIN_GATEWAY_PORT=127.0.0.1:8088
  MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/
  MINIADMIN_BUILD_RETRIES=2

示例：
  bash deploy.sh
  bash deploy.sh --no-cache
  MINIADMIN_WEB_PORT=8090 bash deploy.sh
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

compose() {
  docker compose \
    --project-directory "$APP_DIR" \
    --env-file "$ENV_FILE" \
    "$@"
}

show_failure_context() {
  [[ "$DEPLOY_ACTIVE" == "true" ]] || return 0
  command -v docker >/dev/null 2>&1 || return 0
  docker info >/dev/null 2>&1 || return 0

  printf '\n'
  warn "容器状态："
  compose ps || true

  case "$CURRENT_SERVICE" in
    mysql|redis)
      warn "${CURRENT_SERVICE} 最近日志："
      compose logs --tail=120 "$CURRENT_SERVICE" || true
      ;;
    api)
      warn "API 及数据依赖最近日志："
      compose logs --tail=160 api mysql redis || true
      ;;
    gateway)
      warn "网关及 API 最近日志："
      compose logs --tail=120 gateway api || true
      ;;
    web)
      warn "前端及网关最近日志："
      compose logs --tail=120 web gateway || true
      ;;
  esac
}

on_error() {
  local exit_code="$?"
  local line_number="$1"
  trap - ERR
  printf '\n'
  printf '\033[1;31m[%s]\033[0m 部署失败：阶段=%s，行=%s，退出码=%s\n' \
    "$APP_NAME" "$CURRENT_STAGE" "$line_number" "$exit_code" >&2
  show_failure_context
  printf '\n排查命令：\n  cd %s\n  docker compose ps\n  docker compose logs --tail=200 %s\n' \
    "$APP_DIR" "${CURRENT_SERVICE:-api}" >&2
  exit "$exit_code"
}

trap 'on_error "$LINENO"' ERR

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
      --no-cache)
        NO_CACHE="true"
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
        fail "未知参数：$1"
        ;;
    esac
  done
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || fail "缺少命令：$1"
}

random_hex() {
  local byte_count="$1"
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex "$byte_count"
    return
  fi

  need_command od
  od -An -N "$byte_count" -tx1 /dev/urandom | tr -d ' \n'
}

load_env_value() {
  local key="$1"
  local default_value="$2"
  local value
  value="$(grep -E "^${key}=" "$ENV_FILE" 2>/dev/null | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%$'\r'}"
  value="${value#\"}"
  value="${value%\"}"
  if [[ -z "$value" ]]; then
    printf '%s' "$default_value"
  else
    printf '%s' "$value"
  fi
}

has_placeholder_env() {
  [[ -f "$ENV_FILE" ]] && grep -Eqi 'change_me|replace_with|replace_mysql|replace_redis' "$ENV_FILE"
}

guard_env_regeneration() {
  [[ -f "$ENV_FILE" ]] || return 0
  local mysql_volume
  mysql_volume="$(load_env_value MINIADMIN_MYSQL_VOLUME "miniadmin_mysql")"
  if docker volume inspect "$mysql_volume" >/dev/null 2>&1; then
    fail "检测到已有 MySQL 数据卷 ${mysql_volume}，不能自动更换数据库密码。若这是无数据的失败安装，请先执行 docker compose down -v；若已有数据，请保留原数据库密码并手工修改 .env。"
  fi
}

write_env_file() {
  local web_port="${MINIADMIN_WEB_PORT:-$DEFAULT_WEB_PORT}"
  local api_bind="${MINIADMIN_HTTP_PORT:-$DEFAULT_API_BIND}"
  local gateway_bind="${MINIADMIN_GATEWAY_PORT:-$DEFAULT_GATEWAY_BIND}"
  local jwt_key mysql_password mysql_root_password redis_password
  local public_origin public_port server_ip allow_insecure_http

  jwt_key="$(random_hex 40)"
  mysql_password="$(random_hex 24)"
  mysql_root_password="$(random_hex 24)"
  redis_password="$(random_hex 24)"
  public_port="$(binding_port "$web_port")"
  server_ip="$(detect_server_ip)"
  public_origin="${MINIADMIN_PUBLIC_ORIGIN:-http://${server_ip}:${public_port}/}"
  public_origin="${public_origin%/}/"
  allow_insecure_http="${MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP:-false}"
  if [[ "$public_origin" == http://* ]]; then
    allow_insecure_http="true"
  fi

  cat > "$ENV_FILE" <<EOF
MINIADMIN_HTTP_PORT=${api_bind}
MINIADMIN_GATEWAY_PORT=${gateway_bind}
MINIADMIN_WEB_PORT=${web_port}

MINIADMIN_MYSQL_IMAGE=${MINIADMIN_MYSQL_IMAGE:-mysql:8.4}
MINIADMIN_REDIS_IMAGE=${MINIADMIN_REDIS_IMAGE:-redis:7.4-alpine}
MINIADMIN_DOTNET_SDK_IMAGE=${MINIADMIN_DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}
MINIADMIN_DOTNET_ASPNET_IMAGE=${MINIADMIN_DOTNET_ASPNET_IMAGE:-mcr.microsoft.com/dotnet/aspnet:10.0}
MINIADMIN_NODE_IMAGE=${MINIADMIN_NODE_IMAGE:-node:24-alpine}
MINIADMIN_NGINX_IMAGE=${MINIADMIN_NGINX_IMAGE:-nginx:1.27-alpine}

MINIADMIN_GATEWAY_RATE_LIMITING_ENABLED=${MINIADMIN_GATEWAY_RATE_LIMITING_ENABLED:-true}
MINIADMIN_GATEWAY_RATE_LIMITING_PERMIT_LIMIT=${MINIADMIN_GATEWAY_RATE_LIMITING_PERMIT_LIMIT:-1200}
MINIADMIN_GATEWAY_RATE_LIMITING_WINDOW_SECONDS=${MINIADMIN_GATEWAY_RATE_LIMITING_WINDOW_SECONDS:-60}
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_PERMIT_LIMIT=${MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_PERMIT_LIMIT:-20}
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_WINDOW_SECONDS=${MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_WINDOW_SECONDS:-60}
MINIADMIN_GATEWAY_CANARY_ENABLED=${MINIADMIN_GATEWAY_CANARY_ENABLED:-false}
MINIADMIN_GATEWAY_CANARY_PERCENTAGE=${MINIADMIN_GATEWAY_CANARY_PERCENTAGE:-0}
MINIADMIN_CANARY_API_ADDRESS=${MINIADMIN_CANARY_API_ADDRESS:-http://api:8080/}
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_ENABLED=${MINIADMIN_GATEWAY_CIRCUIT_BREAKER_ENABLED:-true}
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_FAILURE_THRESHOLD=${MINIADMIN_GATEWAY_CIRCUIT_BREAKER_FAILURE_THRESHOLD:-5}
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_BREAK_SECONDS=${MINIADMIN_GATEWAY_CIRCUIT_BREAKER_BREAK_SECONDS:-30}

MINIADMIN_NPM_REGISTRY=${MINIADMIN_NPM_REGISTRY:-https://registry.npmmirror.com}
MINIADMIN_PNPM_FETCH_TIMEOUT=${MINIADMIN_PNPM_FETCH_TIMEOUT:-900000}
MINIADMIN_PNPM_FETCH_RETRIES=${MINIADMIN_PNPM_FETCH_RETRIES:-8}
MINIADMIN_PNPM_NETWORK_CONCURRENCY=${MINIADMIN_PNPM_NETWORK_CONCURRENCY:-2}

MINIADMIN_JWT_SIGNING_KEY=${jwt_key}
MINIADMIN_PUBLIC_ORIGIN=${public_origin}
MINIADMIN_OPEN_PLATFORM_ISSUER=${MINIADMIN_OPEN_PLATFORM_ISSUER:-$public_origin}
MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=${allow_insecure_http}

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

validate_env_file() {
  local key value
  for key in \
    MINIADMIN_JWT_SIGNING_KEY \
    MINIADMIN_MYSQL_PASSWORD \
    MINIADMIN_MYSQL_ROOT_PASSWORD \
    MINIADMIN_REDIS_PASSWORD; do
    value="$(load_env_value "$key" "")"
    [[ -n "$value" ]] || fail ".env 缺少 ${key}，请执行 bash deploy.sh --force-env。"
    if [[ "$value" == *change_me* || "$value" == *replace_* ]]; then
      fail ".env 中 ${key} 仍是占位值，请执行 bash deploy.sh --force-env。"
    fi
  done

  value="$(load_env_value MINIADMIN_JWT_SIGNING_KEY "")"
  if (( ${#value} < 32 )); then
    fail "MINIADMIN_JWT_SIGNING_KEY 至少需要 32 个字符。"
  fi
}

ensure_env_file() {
  if [[ "$FORCE_ENV" == "true" && -f "$ENV_FILE" ]]; then
    guard_env_regeneration
    local backup="${ENV_FILE}.bak.$(date +%Y%m%d%H%M%S)"
    cp "$ENV_FILE" "$backup"
    warn "现有 .env 已备份到 ${backup}"
    write_env_file
    log ".env 已重新生成。"
  elif has_placeholder_env; then
    guard_env_regeneration
    local backup="${ENV_FILE}.bak.$(date +%Y%m%d%H%M%S)"
    cp "$ENV_FILE" "$backup"
    warn "检测到占位配置，原 .env 已备份到 ${backup}"
    write_env_file
    log ".env 已使用随机密钥重新生成。"
  elif [[ ! -f "$ENV_FILE" ]]; then
    write_env_file
    log ".env 已创建，JWT、MySQL、Redis 密码均为随机值。"
  else
    log "使用现有 .env，不覆盖已有密码。"
  fi

  validate_env_file
}

resource_preflight() {
  local available_memory_mb available_disk_mb cpu_count
  cpu_count="$(getconf _NPROCESSORS_ONLN 2>/dev/null || printf 'unknown')"

  if [[ -r /proc/meminfo ]]; then
    available_memory_mb="$(awk '/MemAvailable:/ {printf "%d", $2 / 1024}' /proc/meminfo)"
    if [[ -n "$available_memory_mb" && "$available_memory_mb" -lt 3500 ]]; then
      warn "当前可用内存约 ${available_memory_mb} MB，前端首次构建可能内存不足；建议至少保留 4 GB 可用内存。"
    fi
  else
    available_memory_mb="unknown"
  fi

  available_disk_mb="$(df -Pm "$APP_DIR" | awk 'NR == 2 {print $4}')"
  if [[ -n "$available_disk_mb" && "$available_disk_mb" -lt 6000 ]]; then
    warn "当前可用磁盘约 ${available_disk_mb} MB，首次构建建议至少预留 8 GB。"
  fi

  log "服务器资源：CPU=${cpu_count}，可用内存=${available_memory_mb} MB，可用磁盘=${available_disk_mb} MB。"
}

preflight() {
  [[ -f "${APP_DIR}/docker-compose.yml" ]] || fail "未找到 docker-compose.yml，请在完整 MiniAdmin 目录中执行。"
  need_command docker
  docker compose version >/dev/null 2>&1 || fail "Docker Compose 插件不可用，请先在 1Panel 安装 Docker/Compose。"
  docker info >/dev/null 2>&1 || fail "Docker 服务未运行，请先在 1Panel 启动 Docker。"

  if [[ "$RUN_GIT_PULL" == "true" ]]; then
    if [[ -d "${APP_DIR}/.git" ]]; then
      need_command git
      CURRENT_STAGE="拉取代码"
      log "拉取最新代码..."
      git -C "$APP_DIR" pull --ff-only
    else
      warn "当前是上传的部署包，不是 Git 仓库，已跳过 --pull。"
    fi
  fi

  resource_preflight
}

build_service() {
  local service="$1"
  local retries="${MINIADMIN_BUILD_RETRIES:-$DEFAULT_BUILD_RETRIES}"
  local attempt
  local -a build_args=(build)

  if [[ "$NO_CACHE" == "true" ]]; then
    build_args+=(--no-cache)
  fi
  build_args+=("$service")

  CURRENT_SERVICE="$service"
  for ((attempt = 1; attempt <= retries; attempt++)); do
    log "构建 ${service} 镜像（${attempt}/${retries}）..."
    if compose "${build_args[@]}"; then
      return 0
    fi
    if (( attempt < retries )); then
      warn "${service} 构建失败，15 秒后重试；依赖下载缓存会继续复用。"
      sleep 15
    fi
  done

  return 1
}

ensure_existing_images() {
  local service image_id
  for service in api gateway web; do
    image_id="$(compose images -q "$service" 2>/dev/null || true)"
    [[ -n "$image_id" ]] || fail "服务器不存在 ${service} 镜像，不能使用 --skip-build。"
  done
}

wait_for_service() {
  local service="$1"
  local timeout_seconds="$2"
  local deadline container_id state health

  CURRENT_SERVICE="$service"
  deadline=$(( $(date +%s) + timeout_seconds ))
  while (( $(date +%s) < deadline )); do
    container_id="$(compose ps -q "$service" 2>/dev/null || true)"
    if [[ -z "$container_id" ]]; then
      sleep 3
      continue
    fi

    state="$(docker inspect --format '{{.State.Status}}' "$container_id" 2>/dev/null || true)"
    health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id" 2>/dev/null || true)"

    if [[ "$state" == "running" && ( "$health" == "healthy" || "$health" == "none" ) ]]; then
      log "${service} 已就绪。"
      return 0
    fi

    if [[ "$state" == "exited" || "$state" == "dead" ]]; then
      warn "${service} 已退出。"
      return 1
    fi

    sleep 3
  done

  warn "等待 ${service} 健康检查超时（${timeout_seconds} 秒）。"
  return 1
}

start_compose_services() {
  local attempt
  for attempt in 1 2; do
    if compose up -d --no-build "$@"; then
      return 0
    fi
    if (( attempt < 2 )); then
      warn "容器启动或镜像拉取失败，15 秒后重试。"
      sleep 15
    fi
  done
  return 1
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

wait_for_http() {
  local url="$1"
  local label="$2"
  local max_attempts="${3:-20}"
  local attempt

  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then
      log "${label} 正常：${url}"
      return 0
    fi
    sleep 3
  done

  warn "${label} 无法访问：${url}"
  return 1
}

detect_server_ip() {
  local ip
  ip="$(hostname -I 2>/dev/null | awk '{print $1}' || true)"
  [[ -n "$ip" ]] || ip="SERVER_IP"
  printf '%s' "$ip"
}

build_images() {
  if [[ "$SKIP_BUILD" == "true" ]]; then
    CURRENT_STAGE="检查现有镜像"
    ensure_existing_images
    log "跳过镜像构建。"
    return
  fi

  CURRENT_STAGE="构建 API 镜像"
  build_service api
  CURRENT_STAGE="构建网关镜像"
  build_service gateway
  CURRENT_STAGE="构建前端镜像"
  build_service web
}

start_services() {
  CURRENT_STAGE="启动 MySQL 和 Redis"
  CURRENT_SERVICE="mysql"
  start_compose_services mysql redis
  wait_for_service mysql 300
  wait_for_service redis 180

  CURRENT_STAGE="启动 API 并初始化数据库"
  CURRENT_SERVICE="api"
  start_compose_services api
  wait_for_service api 480

  CURRENT_STAGE="启动网关"
  CURRENT_SERVICE="gateway"
  start_compose_services gateway
  wait_for_service gateway 180

  CURRENT_STAGE="启动前端"
  CURRENT_SERVICE="web"
  start_compose_services web
  wait_for_service web 180
}

verify_stack() {
  if ! command -v curl >/dev/null 2>&1; then
    warn "服务器没有 curl，已跳过宿主机 HTTP 验证；容器健康检查均已通过。"
    return
  fi

  local api_bind gateway_bind web_bind api_host gateway_host web_host
  local api_port gateway_port web_port
  api_bind="$(load_env_value MINIADMIN_HTTP_PORT "$DEFAULT_API_BIND")"
  gateway_bind="$(load_env_value MINIADMIN_GATEWAY_PORT "$DEFAULT_GATEWAY_BIND")"
  web_bind="$(load_env_value MINIADMIN_WEB_PORT "$DEFAULT_WEB_PORT")"
  api_host="$(binding_check_host "$api_bind")"
  gateway_host="$(binding_check_host "$gateway_bind")"
  web_host="$(binding_check_host "$web_bind")"
  api_port="$(binding_port "$api_bind")"
  gateway_port="$(binding_port "$gateway_bind")"
  web_port="$(binding_port "$web_bind")"

  CURRENT_STAGE="验证 API"
  CURRENT_SERVICE="api"
  wait_for_http "http://${api_host}:${api_port}/health" "API 健康检查"

  CURRENT_STAGE="验证网关"
  CURRENT_SERVICE="gateway"
  wait_for_http "http://${gateway_host}:${gateway_port}/health" "网关健康检查"
  wait_for_http "http://${gateway_host}:${gateway_port}/api/health" "网关到 API 链路"

  CURRENT_STAGE="验证前端完整链路"
  CURRENT_SERVICE="web"
  wait_for_http "http://${web_host}:${web_port}/" "前端页面"
  wait_for_http "http://${web_host}:${web_port}/api/health" "前端到网关到 API 链路"
  wait_for_http "http://${web_host}:${web_port}/.well-known/openid-configuration" "OIDC 发现文档"
}

print_summary() {
  local web_bind web_bind_host web_port server_ip visit_host issuer
  web_bind="$(load_env_value MINIADMIN_WEB_PORT "$DEFAULT_WEB_PORT")"
  web_port="$(binding_port "$web_bind")"
  server_ip="$(detect_server_ip)"
  visit_host="$server_ip"
  if [[ "$web_bind" == *:* ]]; then
    web_bind_host="${web_bind%:*}"
    case "$web_bind_host" in
      ""|"0.0.0.0"|"::"|"[::]") visit_host="$server_ip" ;;
      *) visit_host="$web_bind_host" ;;
    esac
  fi
  issuer="$(load_env_value MINIADMIN_OPEN_PLATFORM_ISSUER "http://${visit_host}:${web_port}/")"

  cat <<EOF

MiniAdmin 部署成功。

访问地址：
  Web:            http://${visit_host}:${web_port}
  1Panel 反向代理: http://127.0.0.1:${web_port}
  OIDC Issuer:    ${issuer}

默认账号：
  平台管理员：租户留空，用户名 admin，密码 123456
  演示租户：租户 demo，用户名 demo，密码 123456

请在首次登录后立即修改默认密码。
生产密钥保存在 ${ENV_FILE}，不要上传或提交该文件。

常用命令：
  cd ${APP_DIR}
  docker compose ps
  docker compose logs -f api
  docker compose logs -f gateway
  docker compose logs -f web
  docker compose restart
  docker compose down
EOF
}

deploy() {
  cd "$APP_DIR"
  CURRENT_STAGE="生成并校验环境变量"
  ensure_env_file
  DEPLOY_ACTIVE="true"

  CURRENT_STAGE="校验 Compose 配置"
  log "校验 Docker Compose 配置..."
  compose config --quiet

  export DOCKER_BUILDKIT=1
  export COMPOSE_DOCKER_CLI_BUILD=1
  export COMPOSE_PARALLEL_LIMIT=1

  build_images
  start_services
  verify_stack

  CURRENT_STAGE="部署完成"
  CURRENT_SERVICE=""
  compose ps
  print_summary

  if [[ "$SHOW_LOGS" == "true" ]]; then
    compose logs -f
  fi
}

parse_args "$@"
preflight
deploy
