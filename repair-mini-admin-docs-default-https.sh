#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_SCRIPT="$SCRIPT_DIR/deploy-mini-admin-docs.sh"
TARGET_DOMAIN="${MINIADMIN_DOCS_DOMAIN:-}"

usage_repair() {
  cat <<'EOF'
为现有 MiniAdmin 文档站增加标准 HTTPS 443 域名绑定。

该脚本不会抢占或替换其他网站。1Panel OpenResty 会按 SNI/Host 将
mini.bluecatit.top 路由到文档站，其他域名继续使用原有网站配置。

用法：
  bash repair-mini-admin-docs-default-https.sh --domain mini.bluecatit.top

参数：
  --domain DOMAIN       文档域名，必填
  --onepanel-url URL    1Panel 地址；默认通过 1pctl 自动识别
  --onepanel-api-version VERSION
                       支持 auto、v1、v2，默认 auto
  --onepanel-insecure   允许本机自签名 HTTPS 面板地址
  -h, --help            显示帮助

1Panel API Key 不接受命令行参数，脚本会隐藏输入。
EOF
}

[[ -r "$DEPLOY_SCRIPT" ]] || {
  printf '缺少 %s，请先下载最新版部署脚本到同一目录。\n' "$DEPLOY_SCRIPT" >&2
  exit 1
}

# Reuse the tested 1Panel API discovery and request-signing implementation.
source "$DEPLOY_SCRIPT"

while (($# > 0)); do
  case "$1" in
    --domain)
      [[ $# -ge 2 ]] || fail "--domain 缺少域名。"
      TARGET_DOMAIN="$2"
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
    --onepanel-insecure)
      ONEPANEL_INSECURE=1
      shift
      ;;
    -h|--help)
      usage_repair
      exit 0
      ;;
    *) fail "未知参数：$1" ;;
  esac
done

[[ -n "$TARGET_DOMAIN" ]] || fail "必须通过 --domain 指定文档域名。"
[[ "$TARGET_DOMAIN" == *.* ]] || fail "域名格式不正确：$TARGET_DOMAIN"
[[ "$TARGET_DOMAIN" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]] || fail "域名格式不正确：$TARGET_DOMAIN"

DOMAIN="${TARGET_DOMAIN,,}"
AUTO_SSL=1
SITE_PORT=auto
PORT=8090

prepare_auto_ssl
find_onepanel_website
[[ "$WEBSITE_ID" =~ ^[1-9][0-9]*$ ]] || fail "1Panel 中没有找到 $DOMAIN 的现有文档网站，请先完成 8443 部署。"

onepanel_api GET "/websites/$WEBSITE_ID"
site_type="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.type // empty')"
site_proxy="$(printf '%s' "$ONEPANEL_RESPONSE" | jq -r '.data.proxy // empty')"
[[ "$site_type" == "proxy" ]] || fail "$DOMAIN 对应的网站不是反向代理，未做任何修改。"
[[ "$site_proxy" == "http://127.0.0.1:8090" || "$site_proxy" == "127.0.0.1:8090" ]] || fail "$DOMAIN 当前代理目标是 $site_proxy，不是文档站 127.0.0.1:8090，未做任何修改。"

onepanel_api GET "/websites/domains/$WEBSITE_ID"
existing_binding="$(
  printf '%s' "$ONEPANEL_RESPONSE" |
    jq -c --arg domain "$DOMAIN" '[.data[]? | select(.domain == $domain and .port == 443)] | first // empty'
)"
existing_binding_id="$(printf '%s' "$existing_binding" | jq -r '.id // empty' 2>/dev/null || true)"
existing_binding_ssl="$(printf '%s' "$existing_binding" | jq -r '.ssl // false' 2>/dev/null || true)"
updated_existing_binding=0
if [[ "$existing_binding_id" =~ ^[1-9][0-9]*$ && "$existing_binding_ssl" == "true" ]]; then
  success "$DOMAIN:443 已绑定到文档网站，无需重复修改。"
  exit 0
fi

if [[ "$existing_binding_id" =~ ^[1-9][0-9]*$ ]]; then
  log "$DOMAIN:443 已存在，正在仅启用该绑定的 SSL 标志。"
  body="$(jq -cn --argjson id "$existing_binding_id" '{id:$id,ssl:true}')"
  onepanel_api POST "/websites/domains/update" "$body"
  updated_existing_binding=1
else
  log "为文档网站增加 $DOMAIN:443 HTTPS 绑定；其他域名和网站配置保持不变。"
  body="$(
    jq -cn --argjson websiteId "$WEBSITE_ID" --arg domain "$DOMAIN" '{
      websiteID: $websiteId,
      domains: [{domain: $domain, port: 443, ssl: true}]
    }'
  )"
  onepanel_api POST "/websites/domains" "$body"
fi

onepanel_api GET "/websites/domains/$WEBSITE_ID"
created_binding_id="$(
  printf '%s' "$ONEPANEL_RESPONSE" |
    jq -r --arg domain "$DOMAIN" '[.data[]? | select(.domain == $domain and .port == 443 and .ssl == true)] | first | .id // empty'
)"
[[ "$created_binding_id" =~ ^[1-9][0-9]*$ ]] || fail "1Panel 未确认 $DOMAIN:443 域名绑定。"

status="$(
  curl --noproxy '*' --insecure --silent --show-error \
    --connect-timeout 5 --max-time 15 \
    --resolve "$DOMAIN:443:127.0.0.1" \
    --output /dev/null --write-out '%{http_code}' \
    "https://$DOMAIN/" || true
)"
if [[ ! "$status" =~ ^(200|204|301|302|304|307|308)$ ]]; then
  warn "本机 HTTPS 验证失败（HTTP ${status:-无响应}），正在回滚本次 443 变更。"
  if [[ "$updated_existing_binding" -eq 1 ]]; then
    rollback_body="$(jq -cn --argjson id "$created_binding_id" '{id:$id,ssl:false}')"
    onepanel_api POST "/websites/domains/update" "$rollback_body"
  else
    rollback_body="$(jq -cn --argjson id "$created_binding_id" '{id:$id}')"
    onepanel_api POST "/websites/domains/del" "$rollback_body"
  fi
  fail "443 绑定未通过验证，已回滚；sub2api 和其他网站未被修改。"
fi

success "$DOMAIN 已通过 SNI 共享 443，验证返回 HTTP $status。"
printf '现在可直接访问：https://%s\n' "$DOMAIN"
printf '原 8443 地址仍然保留：https://%s:8443\n' "$DOMAIN"
