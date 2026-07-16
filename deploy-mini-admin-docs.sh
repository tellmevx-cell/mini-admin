#!/usr/bin/env bash
set -Eeuo pipefail

APP_NAME="MiniAdmin Docs"
DOMAIN="${MINIADMIN_DOCS_DOMAIN:-}"
ARCHIVE="${MINIADMIN_DOCS_ARCHIVE:-}"
PORT="${MINIADMIN_DOCS_PORT:-8090}"
BIND_ADDRESS="${MINIADMIN_DOCS_BIND:-127.0.0.1}"
INSTALL_DIR="${MINIADMIN_DOCS_DIR:-/opt/mini-admin-docs}"
IMAGE="${MINIADMIN_DOCS_IMAGE:-nginx:1.27-alpine}"
CONTAINER_NAME="${MINIADMIN_DOCS_CONTAINER:-mini-admin-docs-site}"
FORCE_PULL=0
AUTO_SSL="${MINIADMIN_DOCS_AUTO_SSL:-0}"
ACME_EMAIL="${MINIADMIN_ACME_EMAIL:-}"
CLOUDFLARE_EMAIL="${MINIADMIN_CLOUDFLARE_EMAIL:-}"
CLOUDFLARE_TOKEN="${MINIADMIN_CLOUDFLARE_TOKEN:-}"
ONEPANEL_URL="${MINIADMIN_1PANEL_URL:-}"
ONEPANEL_API_KEY="${MINIADMIN_1PANEL_API_KEY:-}"
ONEPANEL_API_VERSION="${MINIADMIN_1PANEL_API_VERSION:-auto}"
ONEPANEL_INSECURE="${MINIADMIN_1PANEL_INSECURE:-0}"
SITE_PORT="${MINIADMIN_1PANEL_SITE_PORT:-auto}"
SSL_WAIT_SECONDS="${MINIADMIN_SSL_WAIT_SECONDS:-900}"

RELEASES_DIR=""
CURRENT_LINK=""
NGINX_CONFIG=""
NEW_RELEASE=""
LIST_FILE=""
VERBOSE_LIST_FILE=""
DEPLOY_COMPLETE=0
ONEPANEL_RESPONSE=""
ONEPANEL_API_PREFIX=""
ONEPANEL_AUTH_MODE=""
ONEPANEL_HTTP_STATUS=""
ONEPANEL_CONTENT_TYPE=""
ONEPANEL_CURL_ERROR=""
SENSITIVE_FILES=()
WEBSITE_ID=""
ACME_ACCOUNT_ID=""
DNS_ACCOUNT_ID=""
SSL_ID=""

usage() {
  cat <<'EOF'
MiniAdmin 文档站终端部署脚本

把本脚本和 mini-admin-docs-*.tar.gz 上传到服务器同一目录后执行：
  bash deploy-mini-admin-docs.sh --domain docs.example.com

参数：
  --domain DOMAIN    文档域名，必填，例如 docs.example.com
  --archive FILE     文档压缩包；不传时自动选择当前目录最新的压缩包
  --port PORT        宿主机端口，默认 8090
  --bind ADDRESS     监听地址，默认 127.0.0.1；可选 127.0.0.1 或 0.0.0.0
  --dir PATH         发布目录，默认 /opt/mini-admin-docs
  --image IMAGE      Nginx 镜像，默认 nginx:1.27-alpine
  --container NAME   Docker 容器名，默认 mini-admin-docs-site
  --pull             即使本地已有镜像也重新拉取
  --auto-ssl         调用 1Panel API 自动创建站点、申请证书并启用 HTTPS
  --acme-email EMAIL Let's Encrypt 通知邮箱
  --cloudflare-email Cloudflare 账户邮箱
  --onepanel-url URL 1Panel 地址；本机默认通过 1pctl 自动识别
  --onepanel-api-version VERSION
                     1Panel API 版本，支持 auto、v1、v2，默认 auto
  --site-port PORT   1Panel 站点端口；V2 默认直接使用 HTTPS 443，不使用 80
  --onepanel-insecure
                     允许访问使用自签证书的 1Panel HTTPS 地址
  --ssl-timeout SEC  等待证书签发的秒数，默认 900
  -h, --help         显示帮助

对应环境变量：
  MINIADMIN_DOCS_DOMAIN、MINIADMIN_DOCS_ARCHIVE、MINIADMIN_DOCS_PORT
  MINIADMIN_DOCS_BIND、MINIADMIN_DOCS_DIR、MINIADMIN_DOCS_IMAGE
  MINIADMIN_DOCS_CONTAINER、MINIADMIN_DOCS_AUTO_SSL
  MINIADMIN_ACME_EMAIL、MINIADMIN_CLOUDFLARE_EMAIL
  MINIADMIN_CLOUDFLARE_TOKEN、MINIADMIN_1PANEL_URL
  MINIADMIN_1PANEL_API_KEY、MINIADMIN_1PANEL_API_VERSION
  MINIADMIN_1PANEL_INSECURE、MINIADMIN_1PANEL_SITE_PORT

安全说明：Cloudflare Token 和 1Panel API Key 不提供命令行参数。
启用 --auto-ssl 且未设置环境变量时，脚本会在终端隐藏输入。

全自动 HTTPS 示例：
  bash deploy-mini-admin-docs.sh \
    --domain docs.example.com \
    --auto-ssl \
    --acme-email ops@example.com \
    --cloudflare-email ops@example.com

国内服务器可通过镜像变量指定可访问的 Nginx 镜像：
  MINIADMIN_DOCS_IMAGE=你的镜像仓库/nginx:1.27-alpine \
    bash deploy-mini-admin-docs.sh --domain docs.example.com
EOF
}

log() {
  printf '\033[1;34m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

success() {
  printf '\033[1;32m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

warn() {
  printf '\033[1;33m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

fail() {
  printf '\033[1;31m[%s]\033[0m %s\n' "$APP_NAME" "$*" >&2
  exit 1
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || fail "缺少命令：$1"
}

cleanup() {
  if [[ -n "$LIST_FILE" && -f "$LIST_FILE" ]]; then
    rm -f -- "$LIST_FILE"
  fi
  if [[ -n "$VERBOSE_LIST_FILE" && -f "$VERBOSE_LIST_FILE" ]]; then
    rm -f -- "$VERBOSE_LIST_FILE"
  fi
  local sensitive_file
  for sensitive_file in "${SENSITIVE_FILES[@]}"; do
    if [[ -n "$sensitive_file" && -f "$sensitive_file" ]]; then
      rm -f -- "$sensitive_file"
    fi
  done
  CLOUDFLARE_TOKEN=""
  ONEPANEL_API_KEY=""

  if [[ "$DEPLOY_COMPLETE" -eq 0 && -n "$NEW_RELEASE" && -d "$NEW_RELEASE" ]]; then
    local current_target=""
    if [[ -n "$CURRENT_LINK" && -L "$CURRENT_LINK" ]]; then
      current_target="$(readlink -f -- "$CURRENT_LINK" 2>/dev/null || true)"
    fi
    if [[ "$current_target" != "$NEW_RELEASE" ]]; then
      rm -rf -- "$NEW_RELEASE"
    fi
  fi
}

trap cleanup EXIT

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --domain)
        [[ $# -ge 2 ]] || fail "--domain 缺少域名。"
        DOMAIN="$2"
        shift 2
        ;;
      --archive)
        [[ $# -ge 2 ]] || fail "--archive 缺少文件路径。"
        ARCHIVE="$2"
        shift 2
        ;;
      --port)
        [[ $# -ge 2 ]] || fail "--port 缺少端口。"
        PORT="$2"
        shift 2
        ;;
      --bind)
        [[ $# -ge 2 ]] || fail "--bind 缺少监听地址。"
        BIND_ADDRESS="$2"
        shift 2
        ;;
      --dir)
        [[ $# -ge 2 ]] || fail "--dir 缺少安装目录。"
        INSTALL_DIR="$2"
        shift 2
        ;;
      --image)
        [[ $# -ge 2 ]] || fail "--image 缺少镜像名称。"
        IMAGE="$2"
        shift 2
        ;;
      --container)
        [[ $# -ge 2 ]] || fail "--container 缺少容器名称。"
        CONTAINER_NAME="$2"
        shift 2
        ;;
      --pull)
        FORCE_PULL=1
        shift
        ;;
      --auto-ssl)
        AUTO_SSL=1
        shift
        ;;
      --acme-email)
        [[ $# -ge 2 ]] || fail "--acme-email 缺少邮箱。"
        ACME_EMAIL="$2"
        shift 2
        ;;
      --cloudflare-email)
        [[ $# -ge 2 ]] || fail "--cloudflare-email 缺少邮箱。"
        CLOUDFLARE_EMAIL="$2"
        shift 2
        ;;
      --onepanel-url)
        [[ $# -ge 2 ]] || fail "--onepanel-url 缺少地址。"
        ONEPANEL_URL="$2"
        shift 2
        ;;
      --onepanel-api-version)
        [[ $# -ge 2 ]] || fail "--onepanel-api-version 缺少版本。"
        ONEPANEL_API_VERSION="$2"
        shift 2
        ;;
      --site-port)
        [[ $# -ge 2 ]] || fail "--site-port 缺少端口。"
        SITE_PORT="$2"
        shift 2
        ;;
      --onepanel-insecure)
        ONEPANEL_INSECURE=1
        shift
        ;;
      --ssl-timeout)
        [[ $# -ge 2 ]] || fail "--ssl-timeout 缺少秒数。"
        SSL_WAIT_SECONDS="$2"
        shift 2
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

validate_environment() {
  for command in docker tar curl grep mktemp readlink find; do
    need_command "$command"
  done

  docker info >/dev/null 2>&1 || fail "Docker 服务未运行，或者当前账号没有 Docker 权限。"

  [[ -n "$DOMAIN" ]] || fail "请通过 --domain 指定文档域名，例如 docs.example.com。"
  [[ "$DOMAIN" == *.* ]] || fail "域名格式不正确：$DOMAIN"
  [[ "$DOMAIN" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]] || fail "域名格式不正确：$DOMAIN"

  [[ "$PORT" =~ ^[0-9]+$ ]] || fail "端口必须是数字：$PORT"
  ((PORT >= 1 && PORT <= 65535)) || fail "端口必须在 1-65535 之间：$PORT"

  case "$BIND_ADDRESS" in
    127.0.0.1|0.0.0.0) ;;
    *) fail "--bind 只支持 127.0.0.1 或 0.0.0.0。" ;;
  esac

  [[ "$INSTALL_DIR" = /* ]] || fail "安装目录必须使用绝对路径：$INSTALL_DIR"
  [[ "$INSTALL_DIR" != "/" ]] || fail "安装目录不能是根目录。"
  [[ -n "$IMAGE" ]] || fail "Docker 镜像不能为空。"
  [[ "$CONTAINER_NAME" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]+$ ]] || fail "容器名称格式不正确：$CONTAINER_NAME"
  [[ "$AUTO_SSL" == "0" || "$AUTO_SSL" == "1" ]] || fail "MINIADMIN_DOCS_AUTO_SSL 只支持 0 或 1。"

  if [[ "$BIND_ADDRESS" == "0.0.0.0" ]]; then
    warn "当前配置会把 ${PORT} 暴露到公网，请同时配置服务器防火墙。推荐使用默认的 127.0.0.1。"
  fi
}

ensure_jq() {
  if command -v jq >/dev/null 2>&1; then
    return
  fi

  [[ "$EUID" -eq 0 ]] || fail "自动 HTTPS 需要 jq。请先安装 jq，或使用 root 执行脚本。"
  log "自动 HTTPS 需要 jq，正在通过系统包管理器安装。"
  if command -v apt-get >/dev/null 2>&1; then
    apt-get update
    DEBIAN_FRONTEND=noninteractive apt-get install -y jq
  elif command -v dnf >/dev/null 2>&1; then
    dnf install -y jq
  elif command -v yum >/dev/null 2>&1; then
    yum install -y jq
  elif command -v apk >/dev/null 2>&1; then
    apk add --no-cache jq
  else
    fail "无法自动安装 jq，请先手工安装后重试。"
  fi
  command -v jq >/dev/null 2>&1 || fail "jq 安装失败。"
}

read_required_value() {
  local variable_name="$1" prompt_text="$2" secret="${3:-0}" value=""
  [[ -t 0 ]] || fail "$variable_name 未配置，且当前不是交互式终端。"
  if [[ "$secret" -eq 1 ]]; then
    printf '%s' "$prompt_text" >&2
    IFS= read -r -s value || fail "读取 $variable_name 失败。"
    printf '\n' >&2
  else
    IFS= read -r -p "$prompt_text" value || fail "读取 $variable_name 失败。"
  fi
  [[ -n "$value" ]] || fail "$variable_name 不能为空。"
  printf -v "$variable_name" '%s' "$value"
}

detect_local_onepanel_url() {
  local user_info="" panel_url="" authority="" protocol="" port=""
  command -v 1pctl >/dev/null 2>&1 || return 1
  if ! user_info="$(1pctl user-info 2>/dev/null)"; then
    return 1
  fi

  panel_url="$(printf '%s' "$user_info" | grep -Eo 'https?://[^[:space:]]+' | head -n 1 || true)"
  user_info=""
  [[ -n "$panel_url" ]] || return 1
  protocol="${panel_url%%://*}"
  authority="${panel_url#*://}"
  authority="${authority%%/*}"
  port="${authority##*:}"
  [[ "$protocol" == "http" || "$protocol" == "https" ]] || return 1
  [[ "$port" =~ ^[0-9]+$ ]] || return 1
  ((port >= 1 && port <= 65535)) || return 1

  ONEPANEL_URL="$protocol://127.0.0.1:$port"
  if [[ "$protocol" == "https" && "$ONEPANEL_INSECURE" -eq 0 ]]; then
    ONEPANEL_INSECURE=1
    warn "1Panel 本机接口启用了 HTTPS，将通过回环地址校验证书接口并忽略本机证书域名不匹配。"
  fi
  success "已从 1pctl 自动识别 1Panel 地址：$ONEPANEL_URL。"
}

prepare_auto_ssl() {
  [[ "$AUTO_SSL" -eq 1 ]] || return

  ensure_jq
  if ! command -v python3 >/dev/null 2>&1; then
    need_command openssl
    need_command awk
  fi

  if [[ -z "$ACME_EMAIL" ]]; then
    read_required_value ACME_EMAIL "Let's Encrypt 通知邮箱："
  fi
  if [[ -z "$CLOUDFLARE_EMAIL" ]]; then
    read_required_value CLOUDFLARE_EMAIL "Cloudflare 账户邮箱："
  fi
  if [[ -z "$CLOUDFLARE_TOKEN" ]]; then
    read_required_value CLOUDFLARE_TOKEN "Cloudflare API Token（隐藏输入）：" 1
  fi
  if [[ -z "$ONEPANEL_URL" ]]; then
    if ! detect_local_onepanel_url; then
      read_required_value ONEPANEL_URL "1Panel 地址（例如 http://127.0.0.1:10086）："
    fi
  elif [[ "$ONEPANEL_URL" =~ ^https?://(127\.0\.0\.1|localhost):${PORT}/?$ ]]; then
    warn "$ONEPANEL_URL 是文档站端口，不是 1Panel API 地址，正在自动查找正确端口。"
    if ! detect_local_onepanel_url; then
      fail "无法通过 1pctl 自动识别 1Panel 端口。请执行 1pctl user-info，并把其中的协议和端口传给 --onepanel-url。"
    fi
  fi
  if [[ -z "$ONEPANEL_API_KEY" ]]; then
    read_required_value ONEPANEL_API_KEY "1Panel API Key（隐藏输入）：" 1
  fi

  [[ "$ACME_EMAIL" =~ ^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$ ]] || fail "Let's Encrypt 邮箱格式不正确。"
  [[ "$CLOUDFLARE_EMAIL" =~ ^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$ ]] || fail "Cloudflare 邮箱格式不正确。"
  [[ "$CLOUDFLARE_TOKEN" =~ ^[A-Za-z0-9_-]+$ ]] || fail "Cloudflare API Token 格式不正确。"
  [[ "$ONEPANEL_API_KEY" =~ ^[^[:space:]]+$ ]] || fail "1Panel API Key 不能包含空白字符。"
  [[ "$SSL_WAIT_SECONDS" =~ ^[0-9]+$ ]] || fail "--ssl-timeout 必须是数字。"
  ((SSL_WAIT_SECONDS >= 60 && SSL_WAIT_SECONDS <= 3600)) || fail "--ssl-timeout 必须在 60-3600 秒之间。"
  [[ "$ONEPANEL_INSECURE" == "0" || "$ONEPANEL_INSECURE" == "1" ]] || fail "MINIADMIN_1PANEL_INSECURE 只支持 0 或 1。"
  [[ "$ONEPANEL_API_VERSION" == "auto" || "$ONEPANEL_API_VERSION" == "v1" || "$ONEPANEL_API_VERSION" == "v2" ]] || fail "1Panel API 版本只支持 auto、v1 或 v2。"
  if [[ "$SITE_PORT" != "auto" ]]; then
    [[ "$SITE_PORT" =~ ^[0-9]+$ ]] || fail "--site-port 必须是端口数字或 auto。"
    ((SITE_PORT >= 1 && SITE_PORT <= 65535)) || fail "--site-port 必须在 1-65535 之间。"
    [[ "$SITE_PORT" -ne 80 ]] || fail "自动 HTTPS 模式不会使用宿主机 80 端口。"
    [[ "$SITE_PORT" -ne "$PORT" ]] || fail "1Panel 站点端口不能与文档容器端口 $PORT 相同。"
  fi

  ONEPANEL_URL="${ONEPANEL_URL%/}"
  [[ "$ONEPANEL_URL" =~ ^https?://(\[[0-9A-Fa-f:]+\]|[A-Za-z0-9.-]+)(:[0-9]{1,5})?$ ]] || fail "1Panel 地址只填写协议、主机和端口，不要包含安全入口路径：$ONEPANEL_URL"

  detect_onepanel_api
  verify_cloudflare_token
}

sign_onepanel_request() {
  local timestamp="$1" auth_mode="${2:-$ONEPANEL_AUTH_MODE}" signature=""
  if command -v python3 >/dev/null 2>&1; then
    if [[ "$auth_mode" == "md5" ]]; then
      signature="$(
        ONEPANEL_SIGNING_KEY="$ONEPANEL_API_KEY" \
        ONEPANEL_SIGNING_TIMESTAMP="$timestamp" \
          python3 -c 'import hashlib, os; print(hashlib.md5(("1panel" + os.environ["ONEPANEL_SIGNING_KEY"] + os.environ["ONEPANEL_SIGNING_TIMESTAMP"]).encode()).hexdigest())'
      )"
    else
      signature="$(
        ONEPANEL_HMAC_KEY="$ONEPANEL_API_KEY" \
        ONEPANEL_HMAC_DATA="1panel:$timestamp" \
          python3 -c 'import hashlib, hmac, os; print(hmac.new(os.environ["ONEPANEL_HMAC_KEY"].encode(), os.environ["ONEPANEL_HMAC_DATA"].encode(), hashlib.sha256).hexdigest())'
      )"
    fi
  else
    if [[ "$auth_mode" == "md5" ]]; then
      signature="$(printf '1panel%s%s' "$ONEPANEL_API_KEY" "$timestamp" | openssl dgst -md5 | awk '{print $NF}')"
    else
      signature="$(printf '1panel:%s' "$timestamp" | openssl dgst -sha256 -hmac "$ONEPANEL_API_KEY" | awk '{print $NF}')"
    fi
  fi
  if [[ "$auth_mode" == "md5" ]]; then
    [[ "$signature" =~ ^[0-9a-fA-F]{32}$ ]] || fail "无法生成 1Panel V1 API 签名。"
  else
    [[ "$signature" =~ ^[0-9a-fA-F]{64}$ ]] || fail "无法生成 1Panel V2 API 签名。"
  fi
  printf '%s' "$signature"
}

onepanel_request_raw() {
  local api_prefix="$1" auth_mode="$2" method="$3" path="$4" body="${5:-}"
  local timestamp signature curl_config response_file error_file metadata="" request_ok=0

  timestamp="$(date +%s)"
  signature="$(sign_onepanel_request "$timestamp" "$auth_mode")"
  curl_config="$(mktemp)"
  response_file="$(mktemp)"
  error_file="$(mktemp)"
  chmod 600 "$curl_config" "$response_file" "$error_file"
  SENSITIVE_FILES+=("$curl_config" "$response_file" "$error_file")

  {
    printf 'silent\n'
    printf 'compressed\n'
    printf 'connect-timeout = 10\n'
    printf 'max-time = 600\n'
    printf 'request = "%s"\n' "$method"
    printf 'url = "%s%s%s"\n' "$ONEPANEL_URL" "$api_prefix" "$path"
    printf 'header = "Content-Type: application/json"\n'
    printf 'header = "1Panel-Timestamp: %s"\n' "$timestamp"
    printf 'header = "1Panel-Token: %s"\n' "$signature"
    if [[ "$ONEPANEL_INSECURE" -eq 1 ]]; then
      printf 'insecure\n'
    fi
  } >"$curl_config"

  if [[ "$method" == "GET" ]]; then
    if metadata="$(curl --config "$curl_config" --output "$response_file" --write-out $'%{http_code}\n%{content_type}' 2>"$error_file")"; then
      request_ok=1
    fi
  elif metadata="$(printf '%s' "$body" | curl --config "$curl_config" --data-binary @- --output "$response_file" --write-out $'%{http_code}\n%{content_type}' 2>"$error_file")"; then
    request_ok=1
  fi

  ONEPANEL_RESPONSE="$(<"$response_file")"
  ONEPANEL_CURL_ERROR="$(tr '\r\n' '  ' <"$error_file")"
  metadata="${metadata//$'\r'/}"
  ONEPANEL_HTTP_STATUS="${metadata%%$'\n'*}"
  if [[ "$metadata" == *$'\n'* ]]; then
    ONEPANEL_CONTENT_TYPE="${metadata#*$'\n'}"
  else
    ONEPANEL_CONTENT_TYPE=""
  fi
  rm -f -- "$curl_config" "$response_file" "$error_file"
  [[ "$request_ok" -eq 1 ]]
}

onepanel_probe_summary() {
  local code message content_type
  if [[ -n "$ONEPANEL_CURL_ERROR" ]]; then
    printf '连接失败（%s）' "${ONEPANEL_CURL_ERROR:0:160}"
    return
  fi

  content_type="${ONEPANEL_CONTENT_TYPE:-未知}"
  if ! printf '%s' "$ONEPANEL_RESPONSE" | jq -e 'type == "object"' >/dev/null 2>&1; then
    printf 'HTTP %s，Content-Type %s，响应不是 JSON' "${ONEPANEL_HTTP_STATUS:-未知}" "$content_type"
    return
  fi

  code="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.code // empty')"
  message="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.message // empty' | tr '\r\n' '  ')"
  printf 'HTTP %s，code %s' "${ONEPANEL_HTTP_STATUS:-未知}" "${code:-未知}"
  if [[ -n "$message" ]]; then
    printf '，%s' "${message:0:160}"
  fi
}

set_onepanel_api_version() {
  case "$1" in
    v1)
      ONEPANEL_API_VERSION="v1"
      ONEPANEL_API_PREFIX="/api/v1"
      ONEPANEL_AUTH_MODE="md5"
      ;;
    v2)
      ONEPANEL_API_VERSION="v2"
      ONEPANEL_API_PREFIX="/api/v2"
      ONEPANEL_AUTH_MODE="hmac"
      ;;
    *) fail "不支持的 1Panel API 版本：$1" ;;
  esac
}

detect_onepanel_api() {
  local requested_version="$ONEPANEL_API_VERSION" version prefix mode summary
  local v1_summary="未探测" v2_summary="未探测"
  local probe_body='{"page":1,"pageSize":1,"name":"","orderBy":"created_at","order":"null","websiteGroupId":0,"type":""}'
  local candidates=(v2 v1)

  if [[ "$requested_version" != "auto" ]]; then
    candidates=("$requested_version")
  fi

  log "探测 1Panel API 版本与鉴权配置。"
  for version in "${candidates[@]}"; do
    if [[ "$version" == "v2" ]]; then
      prefix="/api/v2"
      mode="hmac"
    else
      prefix="/api/v1"
      mode="md5"
    fi

    if onepanel_request_raw "$prefix" "$mode" POST "/websites/search" "$probe_body" &&
      [[ "$ONEPANEL_HTTP_STATUS" =~ ^2[0-9][0-9]$ ]] &&
      [[ "$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.code // empty' 2>/dev/null || true)" == "200" ]]; then
      set_onepanel_api_version "$version"
      success "已识别 1Panel ${version^^} API（$ONEPANEL_API_PREFIX）。"
      return
    fi

    summary="$(onepanel_probe_summary)"
    if [[ "$version" == "v2" ]]; then
      v2_summary="$summary"
    else
      v1_summary="$summary"
    fi
  done

  if [[ "$requested_version" == "auto" ]]; then
    fail "无法识别 1Panel API。V2：$v2_summary；V1：$v1_summary。请确认 --onepanel-url 是 1Panel 的协议、端口且不含安全入口，并检查 API Key、127.0.0.1 白名单以及服务器时间。"
  fi
  fail "1Panel ${requested_version^^} API 检查失败：$(onepanel_probe_summary)。可改用 --onepanel-api-version auto，或检查 URL、API Key、IP 白名单和服务器时间。"
}

onepanel_api() {
  local method="$1" path="$2" body="${3:-}" code message diagnostic
  if ! onepanel_request_raw "$ONEPANEL_API_PREFIX" "$ONEPANEL_AUTH_MODE" "$method" "$path" "$body"; then
    fail "无法连接 1Panel API（$ONEPANEL_API_PREFIX$path）：${ONEPANEL_CURL_ERROR:-未知网络错误}"
  fi

  if ! [[ "$ONEPANEL_HTTP_STATUS" =~ ^2[0-9][0-9]$ ]]; then
    diagnostic="$(onepanel_probe_summary)"
    case "$ONEPANEL_HTTP_STATUS" in
      301|302|307|308) diagnostic="$diagnostic；1Panel 地址发生跳转，请核对协议和端口" ;;
      401|403) diagnostic="$diagnostic；请检查 API Key、IP 白名单、有效期和服务器时间" ;;
      404) diagnostic="$diagnostic；API 路径不存在，请检查 1Panel 版本或端口" ;;
    esac
    fail "1Panel API 请求失败（$ONEPANEL_API_PREFIX$path）：$diagnostic"
  fi

  if ! printf '%s' "$ONEPANEL_RESPONSE" | jq -e 'type == "object"' >/dev/null 2>&1; then
    fail "1Panel API 返回 HTTP $ONEPANEL_HTTP_STATUS，但响应不是 JSON（Content-Type: ${ONEPANEL_CONTENT_TYPE:-未知}）。当前地址可能不是 1Panel 面板端口，或包含了错误的反向代理。"
  fi

  code="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.code // empty')"
  if [[ "$code" != "200" ]]; then
    message="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.message // "未返回错误信息"' | tr '\r\n' '  ')"
    if [[ "$code" == "401" || "$code" == "403" ]]; then
      fail "1Panel API 鉴权失败（$ONEPANEL_API_PREFIX$path，code $code）：${message:0:240}。请检查 API Key、白名单、有效期和服务器时间。"
    fi
    fail "1Panel API 业务请求失败（$ONEPANEL_API_PREFIX$path，code ${code:-未知}）：${message:0:240}"
  fi
}

verify_cloudflare_token() {
  local curl_config response
  curl_config="$(mktemp)"
  chmod 600 "$curl_config"
  SENSITIVE_FILES+=("$curl_config")
  {
    printf 'silent\n'
    printf 'show-error\n'
    printf 'compressed\n'
    printf 'connect-timeout = 10\n'
    printf 'max-time = 30\n'
    printf 'url = "https://api.cloudflare.com/client/v4/user/tokens/verify"\n'
    printf 'header = "Authorization: Bearer %s"\n' "$CLOUDFLARE_TOKEN"
  } >"$curl_config"

  if ! response="$(curl --config "$curl_config")"; then
    rm -f -- "$curl_config"
    fail "无法访问 Cloudflare API，请检查服务器网络。"
  fi
  rm -f -- "$curl_config"
  [[ "$(printf '%s' "$response" | jq -r '.success // false')" == "true" ]] || fail "Cloudflare API Token 无效或无法验证。"
  success "Cloudflare API Token 验证通过。"
}

find_onepanel_website() {
  local body
  body="$(
    jq -cn --arg domain "$DOMAIN" '{
      page: 1,
      pageSize: 100,
      name: $domain,
      orderBy: "created_at",
      order: "null",
      websiteGroupId: 0,
      type: ""
    }'
  )"
  onepanel_api POST "/websites/search" "$body"
  WEBSITE_ID="$(
    printf '%s' "$ONEPANEL_RESPONSE" |
      jq -r --arg domain "$DOMAIN" '[.data.items[]? | select((.primaryDomain | split(":")[0]) == $domain)] | first | .id // empty'
  )"
}

port_is_listening() {
  local port="$1"
  if command -v ss >/dev/null 2>&1; then
    ss -H -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ":${port}$"
    return
  fi
  if command -v netstat >/dev/null 2>&1; then
    netstat -lnt 2>/dev/null | awk 'NR > 2 {print $4}' | grep -Eq ":${port}$"
    return
  fi
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    exec 3>&- 3<&-
    return 0
  fi
  return 1
}

select_site_port() {
  local candidate panel_port="${ONEPANEL_URL##*:}"
  if [[ "$SITE_PORT" != "auto" ]]; then
    if [[ "$ONEPANEL_API_VERSION" == "v1" ]] && port_is_listening "$SITE_PORT"; then
      fail "指定的 1Panel V1 过渡端口 $SITE_PORT 已被占用，请更换 --site-port。"
    fi
    if [[ "$ONEPANEL_API_VERSION" == "v2" && "$SITE_PORT" -ne 443 ]]; then
      warn "V2 使用非 443 HTTPS 端口时，需要额外配置 Cloudflare Origin Rule。推荐保持默认 443。"
    fi
    success "将使用指定的非 80 站点端口：$SITE_PORT。"
    return
  fi

  if [[ "$ONEPANEL_API_VERSION" == "v2" ]]; then
    SITE_PORT=443
    if port_is_listening "$SITE_PORT"; then
      log "443 已有监听，1Panel 将检查它是否可由 OpenResty 共享。"
    fi
    success "1Panel V2 将直接创建 HTTPS 443 站点，不绑定 80。"
    return
  fi

  for candidate in 8081 8082 8083 8880 9080 10080 18080; do
    if [[ "$candidate" -eq "$PORT" || "$candidate" == "$panel_port" ]]; then
      continue
    fi
    if ! port_is_listening "$candidate"; then
      SITE_PORT="$candidate"
      success "已为 1Panel V1 选择临时非 80 端口：$SITE_PORT。"
      return
    fi
  done
  fail "未找到可用的 1Panel V1 非 80 过渡端口，请通过 --site-port 指定一个未占用端口。"
}

ensure_onepanel_website() {
  local group_id alias task_id proxy_url body site_type site_proxy primary_domain
  proxy_url="http://127.0.0.1:$PORT"
  find_onepanel_website

  if [[ -z "$WEBSITE_ID" ]]; then
    select_site_port
    log "1Panel 中没有 $DOMAIN，正在创建反向代理网站。"
    onepanel_api POST "/groups/search" '{"type":"website"}'
    group_id="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data[0].id // empty')"
    [[ "$group_id" =~ ^[0-9]+$ ]] || fail "1Panel 中没有可用的网站分组。"

    alias="docs-${DOMAIN//./-}"
    alias="${alias:0:40}"
    if [[ -r /proc/sys/kernel/random/uuid ]]; then
      IFS= read -r task_id </proc/sys/kernel/random/uuid
    elif command -v uuidgen >/dev/null 2>&1; then
      task_id="$(uuidgen)"
    else
      task_id="miniadmin-docs-$(date +%s)-$$"
    fi
    if [[ "$ONEPANEL_API_VERSION" == "v1" ]]; then
      primary_domain="$DOMAIN:$SITE_PORT"
      body="$(
        jq -cn \
          --arg alias "$alias" \
          --arg domain "$primary_domain" \
          --arg proxy "$proxy_url" \
          --argjson groupId "$group_id" \
          '{
            primaryDomain: $domain,
            otherDomains: "",
            type: "proxy",
            alias: $alias,
            remark: "MiniAdmin documentation",
            proxy: $proxy,
            webSiteGroupID: $groupId,
            appType: "installed",
            IPV6: false
          }'
      )"
    else
      body="$(
        jq -cn \
          --arg alias "$alias" \
          --arg domain "$DOMAIN" \
          --arg proxy "$proxy_url" \
          --arg taskId "$task_id" \
          --argjson groupId "$group_id" \
          --argjson sitePort "$SITE_PORT" \
          --argjson sslId "$SSL_ID" \
          '{
            type: "proxy",
            alias: $alias,
            remark: "MiniAdmin documentation",
            proxy: $proxy,
            webSiteGroupID: $groupId,
            appType: "installed",
            IPV6: false,
            domains: [{domain: $domain, port: $sitePort, ssl: true}],
            taskID: $taskId,
            enableSSL: true,
            websiteSSLID: $sslId
          }'
      )"
    fi
    onepanel_api POST "/websites" "$body"
    find_onepanel_website
    [[ "$WEBSITE_ID" =~ ^[1-9][0-9]*$ ]] || fail "1Panel 网站创建完成，但无法查询到 $DOMAIN 的有效网站 ID。"
  fi

  onepanel_api GET "/websites/$WEBSITE_ID"
  site_type="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.type // empty')"
  site_proxy="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.proxy // empty')"
  [[ "$site_type" == "proxy" ]] || fail "域名 $DOMAIN 已被非反向代理网站占用，未自动修改现有网站。"
  if [[ "$site_proxy" != "$proxy_url" && "$site_proxy" != "127.0.0.1:$PORT" ]]; then
    fail "域名 $DOMAIN 已代理到 $site_proxy，预期为 $proxy_url；为避免覆盖现有配置已停止。"
  fi
  success "1Panel 反向代理网站已就绪（ID: $WEBSITE_ID）。"
}

ensure_onepanel_acme_account() {
  local body key_type="EC256" attempt
  if [[ "$ONEPANEL_API_VERSION" == "v1" ]]; then
    key_type="P256"
  fi
  onepanel_api POST "/websites/acme/search" '{"page":1,"pageSize":100}'
  ACME_ACCOUNT_ID="$(
    printf '%s' "$ONEPANEL_RESPONSE" |
      jq -r --arg email "$ACME_EMAIL" '[.data.items[]? | select(.email == $email and .type == "letsencrypt" and (.id // 0) > 0)] | first | .id // empty'
  )"

  if [[ -z "$ACME_ACCOUNT_ID" ]]; then
    log "创建 1Panel Let's Encrypt ACME 账户。"
    body="$(
      jq -cn --arg email "$ACME_EMAIL" --arg keyType "$key_type" '{
        email: $email,
        type: "letsencrypt",
        keyType: $keyType,
        eabKid: "",
        eabHmacKey: "",
        useProxy: false,
        caDirURL: "",
        useEAB: false
      }'
    )"
    onepanel_api POST "/websites/acme" "$body"
    # Some 1Panel V2 releases return a placeholder ID of 0 after creation.
    # Query the persisted account instead of forwarding that invalid ID.
    ACME_ACCOUNT_ID=""
    for attempt in {1..10}; do
      onepanel_api POST "/websites/acme/search" '{"page":1,"pageSize":100}'
      ACME_ACCOUNT_ID="$(
        printf '%s' "$ONEPANEL_RESPONSE" |
          jq -r --arg email "$ACME_EMAIL" '[.data.items[]? | select(.email == $email and .type == "letsencrypt" and (.id // 0) > 0)] | first | .id // empty'
      )"
      [[ "$ACME_ACCOUNT_ID" =~ ^[1-9][0-9]*$ ]] && break
      sleep 1
    done
  fi

  [[ "$ACME_ACCOUNT_ID" =~ ^[1-9][0-9]*$ ]] || fail "无法创建或查询有效的 1Panel ACME 账户 ID（1Panel 返回了空值或 0）。"
  success "Let's Encrypt ACME 账户已就绪（ID: $ACME_ACCOUNT_ID）。"
}

ensure_onepanel_dns_account() {
  local account_name body
  account_name="miniadmin-${DOMAIN//./-}"
  account_name="${account_name:0:40}"

  onepanel_api POST "/websites/dns/search" '{"page":1,"pageSize":100}'
  DNS_ACCOUNT_ID="$(
    printf '%s' "$ONEPANEL_RESPONSE" |
      jq -r --arg name "$account_name" '[.data.items[]? | select(.name == $name and .type == "CloudFlare" and (.id // 0) > 0)] | first | .id // empty'
  )"

  if [[ -z "$DNS_ACCOUNT_ID" ]]; then
    log "创建 1Panel Cloudflare DNS 账户。"
    body="$(
      jq -cn \
        --arg name "$account_name" \
        --arg email "$CLOUDFLARE_EMAIL" \
        --arg token "$CLOUDFLARE_TOKEN" \
        '{name: $name, type: "CloudFlare", authorization: {email: $email, apiKey: $token}}'
    )"
    onepanel_api POST "/websites/dns" "$body"
    onepanel_api POST "/websites/dns/search" '{"page":1,"pageSize":100}'
    DNS_ACCOUNT_ID="$(
      printf '%s' "$ONEPANEL_RESPONSE" |
        jq -r --arg name "$account_name" '[.data.items[]? | select(.name == $name and .type == "CloudFlare" and (.id // 0) > 0)] | first | .id // empty'
    )"
  else
    log "更新现有 1Panel Cloudflare DNS 账户凭证。"
    body="$(
      jq -cn \
        --argjson id "$DNS_ACCOUNT_ID" \
        --arg name "$account_name" \
        --arg email "$CLOUDFLARE_EMAIL" \
        --arg token "$CLOUDFLARE_TOKEN" \
        '{id: $id, name: $name, type: "CloudFlare", authorization: {email: $email, apiKey: $token}}'
    )"
    onepanel_api POST "/websites/dns/update" "$body"
  fi

  [[ "$DNS_ACCOUNT_ID" =~ ^[1-9][0-9]*$ ]] || fail "无法创建或查询有效的 1Panel Cloudflare DNS 账户 ID。"
  success "Cloudflare DNS 账户已就绪（ID: $DNS_ACCOUNT_ID）。"
}

certificate_request_body() {
  local id="${1:-0}" key_type="EC256"
  if [[ "$ONEPANEL_API_VERSION" == "v1" ]]; then
    key_type="P256"
  fi
  jq -cn \
    --argjson id "$id" \
    --arg domain "$DOMAIN" \
    --arg keyType "$key_type" \
    --argjson acmeId "$ACME_ACCOUNT_ID" \
    --argjson dnsId "$DNS_ACCOUNT_ID" \
    '{
      id: $id,
      primaryDomain: $domain,
      otherDomains: "",
      provider: "dnsAccount",
      acmeAccountId: $acmeId,
      dnsAccountId: $dnsId,
      autoRenew: true,
      keyType: $keyType,
      apply: true,
      pushDir: false,
      dir: "",
      description: "MiniAdmin documentation auto TLS",
      disableCNAME: false,
      skipDNS: false,
      nameserver1: "",
      nameserver2: "",
      execShell: false,
      shell: "",
      pushNode: false,
      nodes: "",
      isIp: false
    }'
}

wait_for_onepanel_certificate() {
  local deadline status message
  deadline=$((SECONDS + SSL_WAIT_SECONDS))
  while ((SECONDS < deadline)); do
    onepanel_api GET "/websites/ssl/$SSL_ID"
    status="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.status // empty')"
    case "$status" in
      ready)
        success "Let's Encrypt 证书签发成功（ID: $SSL_ID）。"
        return 0
        ;;
      error|applyError)
        message="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.message // "未知错误"')"
        fail "证书签发失败：$message"
        ;;
      init|applying|"") ;;
      *) warn "证书当前状态：$status" ;;
    esac
    sleep 5
  done
  fail "等待证书签发超过 ${SSL_WAIT_SECONDS} 秒，请到 1Panel 证书日志查看详情。"
}

ensure_onepanel_certificate() {
  local body search_body ssl_record status attempt
  search_body="$(jq -cn --arg domain "$DOMAIN" '{page:1,pageSize:100,acmeAccountID:"",domain:$domain,orderBy:"updated_at",order:"descending"}')"
  onepanel_api POST "/websites/ssl/search" "$search_body"
  ssl_record="$(
    printf '%s' "$ONEPANEL_RESPONSE" |
      jq -c --arg domain "$DOMAIN" '[.data.items[]? | select(.primaryDomain == $domain and .provider == "dnsAccount" and (.id // 0) > 0)] | sort_by(.id) | last // empty'
  )"

  if [[ -z "$ssl_record" ]]; then
    log "向 Let's Encrypt 申请 $DOMAIN 的新证书。"
    body="$(certificate_request_body 0)"
    onepanel_api POST "/websites/ssl" "$body"
    SSL_ID="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.id // empty')"
    status="init"
    if [[ ! "$SSL_ID" =~ ^[1-9][0-9]*$ ]]; then
      SSL_ID=""
      for attempt in {1..10}; do
        onepanel_api POST "/websites/ssl/search" "$search_body"
        ssl_record="$(
          printf '%s' "$ONEPANEL_RESPONSE" |
            jq -c --arg domain "$DOMAIN" '[.data.items[]? | select(.primaryDomain == $domain and .provider == "dnsAccount" and (.id // 0) > 0)] | sort_by(.id) | last // empty'
        )"
        if [[ -n "$ssl_record" ]]; then
          SSL_ID="$(printf '%s' "$ssl_record" | jq -r '.id // empty')"
          status="$(printf '%s' "$ssl_record" | jq -r '.status // "init"')"
        fi
        [[ "$SSL_ID" =~ ^[1-9][0-9]*$ ]] && break
        sleep 1
      done
    fi
  else
    SSL_ID="$(printf '%s' "$ssl_record" | jq -r '.id')"
    status="$(printf '%s' "$ssl_record" | jq -r '.status // empty')"
    if [[ "$status" != "applying" ]]; then
      body="$(certificate_request_body "$SSL_ID")"
      onepanel_api POST "/websites/ssl/update" "$body"
    fi
    if [[ "$status" == "ready" ]]; then
      success "复用现有有效证书（ID: $SSL_ID），并已启用自动续签。"
      return
    fi
    if [[ "$status" != "applying" ]]; then
      log "重新触发证书签发（ID: $SSL_ID）。"
      body="$(jq -cn --argjson id "$SSL_ID" '{ID:$id,skipDNSCheck:false,nameservers:[]}')"
      onepanel_api POST "/websites/ssl/obtain" "$body"
    fi
  fi

  [[ "$SSL_ID" =~ ^[1-9][0-9]*$ ]] || fail "1Panel 未返回有效的证书 ID。"
  wait_for_onepanel_certificate
}

attach_onepanel_certificate() {
  local algorithm body enabled attached_id http_config
  algorithm="ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES128-GCM-SHA256:!aNULL:!eNULL:!EXPORT:!DSS:!DES:!RC4:!3DES:!MD5:!PSK"
  body="$(
    jq -cn \
      --argjson websiteId "$WEBSITE_ID" \
      --argjson sslId "$SSL_ID" \
      --arg algorithm "$algorithm" \
      '{
        websiteId: $websiteId,
        enable: true,
        websiteSSLId: $sslId,
        type: "existed",
        privateKey: "",
        certificate: "",
        privateKeyPath: "",
        certificatePath: "",
        importType: "",
        httpConfig: "HTTPSOnly",
        "SSLProtocol": ["TLSv1.3", "TLSv1.2"],
        algorithm: $algorithm,
        hsts: true,
        hstsIncludeSubDomains: false,
        httpsPorts: [443],
        http3: false
      }'
  )"
  onepanel_api POST "/websites/$WEBSITE_ID/https" "$body"
  onepanel_api GET "/websites/$WEBSITE_ID/https"
  enabled="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.enable // false')"
  attached_id="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.SSL.id // empty')"
  http_config="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.httpConfig // empty')"
  [[ "$enabled" == "true" && "$attached_id" == "$SSL_ID" && "$http_config" == "HTTPSOnly" ]] || fail "1Panel 未确认仅 HTTPS 的证书绑定结果。"
  success "1Panel 已绑定证书并启用仅 HTTPS 模式，未占用 80。"
}

configure_auto_ssl() {
  [[ "$AUTO_SSL" -eq 1 ]] || return
  log "开始配置 1Panel 与 Let's Encrypt 自动 HTTPS。"
  ensure_onepanel_acme_account
  ensure_onepanel_dns_account
  ensure_onepanel_certificate
  ensure_onepanel_website
  attach_onepanel_certificate
}

resolve_archive() {
  if [[ -z "$ARCHIVE" ]]; then
    local candidate
    shopt -s nullglob
    for candidate in "$PWD"/mini-admin-docs-*.tar.gz; do
      if [[ -z "$ARCHIVE" || "$candidate" -nt "$ARCHIVE" ]]; then
        ARCHIVE="$candidate"
      fi
    done
    shopt -u nullglob
  fi

  [[ -n "$ARCHIVE" ]] || fail "当前目录没有 mini-admin-docs-*.tar.gz，请上传压缩包或使用 --archive 指定。"
  [[ -f "$ARCHIVE" && -r "$ARCHIVE" ]] || fail "无法读取文档压缩包：$ARCHIVE"
  ARCHIVE="$(readlink -f -- "$ARCHIVE")"
}

validate_archive() {
  local entry normalized has_index=0
  LIST_FILE="$(mktemp)"
  VERBOSE_LIST_FILE="$(mktemp)"

  tar -tzf "$ARCHIVE" >"$LIST_FILE" || fail "压缩包损坏或不是 tar.gz：$ARCHIVE"
  tar -tvzf "$ARCHIVE" >"$VERBOSE_LIST_FILE" || fail "无法读取压缩包文件类型。"
  [[ -s "$LIST_FILE" ]] || fail "文档压缩包为空。"

  while IFS= read -r entry; do
    normalized="${entry#./}"
    [[ "$normalized" != /* ]] || fail "压缩包包含绝对路径，已拒绝部署：$entry"
    case "/$normalized/" in
      *"/../"*) fail "压缩包包含目录穿越路径，已拒绝部署：$entry" ;;
    esac
    if [[ "$normalized" == "index.html" ]]; then
      has_index=1
    fi
  done <"$LIST_FILE"

  [[ "$has_index" -eq 1 ]] || fail "压缩包根目录缺少 index.html。"
  if grep -Eq '^[lh]' "$VERBOSE_LIST_FILE"; then
    fail "压缩包包含符号链接或硬链接，已拒绝部署。"
  fi
}

prepare_release() {
  RELEASES_DIR="$INSTALL_DIR/releases"
  CURRENT_LINK="$INSTALL_DIR/current"
  NGINX_CONFIG="$INSTALL_DIR/nginx.conf"

  mkdir -p -- "$RELEASES_DIR" || fail "无法创建 $RELEASES_DIR，请使用 root 或有权限的账号执行。"
  if [[ -e "$CURRENT_LINK" && ! -L "$CURRENT_LINK" ]]; then
    fail "$CURRENT_LINK 已存在但不是符号链接，请先人工检查。"
  fi

  NEW_RELEASE="$RELEASES_DIR/$(date +%Y%m%d%H%M%S)-$$"
  mkdir -p -- "$NEW_RELEASE"
  log "解压 $(basename "$ARCHIVE") 到 $NEW_RELEASE。"
  tar --no-same-owner --no-same-permissions -xzf "$ARCHIVE" -C "$NEW_RELEASE"
  [[ -f "$NEW_RELEASE/index.html" ]] || fail "解压后没有找到 index.html。"
  [[ -z "$(find "$NEW_RELEASE" -type l -print -quit)" ]] || fail "解压目录中发现符号链接，已拒绝部署。"
  chmod -R a=rX,u+w -- "$NEW_RELEASE"
}

write_nginx_config() {
  cat >"$NGINX_CONFIG" <<'NGINX'
user nginx;
worker_processes auto;
error_log /dev/stderr notice;
pid /tmp/nginx.pid;

events {
    worker_connections 1024;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;
    access_log /dev/stdout;
    sendfile on;
    keepalive_timeout 65;
    server_tokens off;

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css application/json application/javascript application/xml image/svg+xml;

    server {
        listen 80 default_server;
        server_name _;
        root /usr/share/nginx/html;
        index index.html;

        location = /health {
            access_log off;
            default_type text/plain;
            return 200 "ok\n";
        }

        location / {
            try_files $uri $uri.html $uri/ =404;
            add_header Cache-Control "no-cache, no-store, must-revalidate" always;
            add_header X-Content-Type-Options "nosniff" always;
            add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        }

        location = /404.html {
            internal;
            add_header Cache-Control "no-cache, no-store, must-revalidate" always;
            add_header X-Content-Type-Options "nosniff" always;
            add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        }

        location ~* \.(?:css|js|mjs|png|jpg|jpeg|gif|svg|ico|webp|woff2?)$ {
            try_files $uri =404;
            access_log off;
            add_header Cache-Control "public, max-age=2592000, immutable" always;
            add_header X-Content-Type-Options "nosniff" always;
            add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        }

        location ~* \.html$ {
            try_files $uri =404;
            add_header Cache-Control "no-cache, no-store, must-revalidate" always;
            add_header X-Content-Type-Options "nosniff" always;
            add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        }

        error_page 404 /404.html;
    }
}
NGINX
}

ensure_image() {
  if [[ "$FORCE_PULL" -eq 1 ]]; then
    log "拉取 Docker 镜像 $IMAGE。"
    docker pull "$IMAGE"
  elif docker image inspect "$IMAGE" >/dev/null 2>&1; then
    log "使用本地 Docker 镜像 $IMAGE。"
  else
    log "本地没有 $IMAGE，开始拉取。"
    docker pull "$IMAGE"
  fi
}

validate_nginx_config() {
  log "校验 Nginx 配置和文档目录。"
  docker run --rm \
    --entrypoint nginx \
    --volume "$NGINX_CONFIG:/etc/nginx/nginx.conf:ro" \
    --volume "$NEW_RELEASE:/usr/share/nginx/html:ro" \
    "$IMAGE" -t
}

run_container() {
  local release_dir="$1"
  docker run --detach \
    --name "$CONTAINER_NAME" \
    --restart unless-stopped \
    --label "com.miniadmin.component=docs-site" \
    --publish "$BIND_ADDRESS:$PORT:80" \
    --volume "$NGINX_CONFIG:/etc/nginx/nginx.conf:ro" \
    --volume "$release_dir:/usr/share/nginx/html:ro" \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=16m \
    --tmpfs /var/cache/nginx:rw,noexec,nosuid,size=16m \
    --health-cmd 'wget -q -O - http://127.0.0.1/health >/dev/null || exit 1' \
    --health-interval 10s \
    --health-timeout 3s \
    --health-retries 5 \
    --health-start-period 5s \
    --log-opt max-size=10m \
    --log-opt max-file=3 \
    --entrypoint nginx \
    "$IMAGE" -g 'daemon off;' >/dev/null
}

wait_for_health() {
  local attempt
  for ((attempt = 1; attempt <= 30; attempt++)); do
    if curl --fail --silent --show-error --max-time 2 "http://127.0.0.1:$PORT/health" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

rollback() {
  local previous_release="$1"
  docker rm --force "$CONTAINER_NAME" >/dev/null 2>&1 || true

  if [[ -n "$previous_release" && -f "$previous_release/index.html" ]]; then
    warn "新版本启动失败，正在恢复上一版本：$previous_release"
    if run_container "$previous_release" && wait_for_health; then
      success "上一版本已恢复。"
      return 0
    fi
    docker logs --tail 100 "$CONTAINER_NAME" 2>&1 || true
  fi

  warn "没有可用的上一版本，或上一版本也无法启动。"
  return 1
}

deploy() {
  local previous_release="" managed_label=""
  if [[ -L "$CURRENT_LINK" ]]; then
    previous_release="$(readlink -f -- "$CURRENT_LINK" 2>/dev/null || true)"
  fi

  if docker container inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
    managed_label="$(docker container inspect --format '{{ index .Config.Labels "com.miniadmin.component" }}' "$CONTAINER_NAME" 2>/dev/null || true)"
    [[ "$managed_label" == "docs-site" ]] || fail "容器名 $CONTAINER_NAME 已被其他容器占用，已停止部署。"
    log "停止现有文档容器。"
    docker rm --force "$CONTAINER_NAME" >/dev/null
  fi

  log "启动文档容器，监听 $BIND_ADDRESS:$PORT。"
  if ! run_container "$NEW_RELEASE"; then
    rollback "$previous_release" || true
    fail "Docker 容器启动失败，请检查端口占用和 Docker 日志。"
  fi

  if ! wait_for_health; then
    docker logs --tail 100 "$CONTAINER_NAME" 2>&1 || true
    rollback "$previous_release" || true
    fail "新版本在 30 秒内未通过健康检查，已尝试回滚。"
  fi

  ln -sfn "$NEW_RELEASE" "$CURRENT_LINK"
  DEPLOY_COMPLETE=1
}

print_next_steps() {
  success "文档站部署成功：http://127.0.0.1:$PORT"
  printf '\n服务器验证：\n'
  printf '  curl -I http://127.0.0.1:%s/\n' "$PORT"
  printf '  docker ps --filter name=%s\n' "$CONTAINER_NAME"
  printf '  docker logs --tail=100 %s\n' "$CONTAINER_NAME"
  if [[ "$AUTO_SSL" -eq 1 ]]; then
    printf '\n1Panel 自动配置已完成：\n'
    printf '  - 反向代理：http://127.0.0.1:%s\n' "$PORT"
    printf '  - Let\x27s Encrypt 证书：已签发并启用自动续签\n'
    printf '  - HTTPS：已启用，仅监听 HTTPS 站点端口，不使用宿主机 80\n'
  else
    printf '\n1Panel 后续配置：\n'
    printf '  1. 创建反向代理网站，主域名填写 %s。\n' "$DOMAIN"
    printf '  2. 代理地址填写 http://127.0.0.1:%s。\n' "$PORT"
    printf '  3. 为该网站申请证书并开启 HTTPS。\n'
  fi
  printf '\nCloudflare 后续配置：\n'
  printf '  1. 添加 %s 指向服务器公网 IP 的 A 记录。\n' "$DOMAIN"
  printf '  2. 确认源站 HTTPS 正常后开启橙色云。\n'
  printf '  3. SSL/TLS 模式选择 Full (strict)。\n'
  printf '\n注意：Cloudflare 橙色云不直接代理 8090。公网访问应使用 https://%s，\n' "$DOMAIN"
  printf '由 1Panel 的 443 端口反向代理到本机 %s。\n' "$PORT"
}

main() {
  parse_args "$@"
  validate_environment
  prepare_auto_ssl
  resolve_archive
  validate_archive
  prepare_release
  write_nginx_config
  ensure_image
  validate_nginx_config
  deploy
  configure_auto_ssl
  print_next_steps
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
