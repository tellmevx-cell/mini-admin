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

RELEASES_DIR=""
CURRENT_LINK=""
NGINX_CONFIG=""
NEW_RELEASE=""
LIST_FILE=""
VERBOSE_LIST_FILE=""
DEPLOY_COMPLETE=0

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
  -h, --help         显示帮助

对应环境变量：
  MINIADMIN_DOCS_DOMAIN、MINIADMIN_DOCS_ARCHIVE、MINIADMIN_DOCS_PORT
  MINIADMIN_DOCS_BIND、MINIADMIN_DOCS_DIR、MINIADMIN_DOCS_IMAGE
  MINIADMIN_DOCS_CONTAINER

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

  if [[ "$BIND_ADDRESS" == "0.0.0.0" ]]; then
    warn "当前配置会把 ${PORT} 暴露到公网，请同时配置服务器防火墙。推荐使用默认的 127.0.0.1。"
  fi
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
  printf '\n1Panel 后续配置：\n'
  printf '  1. 创建反向代理网站，主域名填写 %s。\n' "$DOMAIN"
  printf '  2. 代理地址填写 http://127.0.0.1:%s。\n' "$PORT"
  printf '  3. 为该网站申请证书并开启 HTTPS 和 HTTP 跳转 HTTPS。\n'
  printf '\nCloudflare 后续配置：\n'
  printf '  1. 添加 %s 指向服务器公网 IP 的 A 记录。\n' "$DOMAIN"
  printf '  2. 确认源站 HTTPS 正常后开启橙色云。\n'
  printf '  3. SSL/TLS 模式选择 Full (strict)。\n'
  printf '\n注意：Cloudflare 橙色云不直接代理 8090。公网访问应使用 https://%s，\n' "$DOMAIN"
  printf '由 1Panel 的 443 端口反向代理到本机 %s。\n' "$PORT"
}

parse_args "$@"
validate_environment
resolve_archive
validate_archive
prepare_release
write_nginx_config
ensure_image
validate_nginx_config
deploy
print_next_steps
