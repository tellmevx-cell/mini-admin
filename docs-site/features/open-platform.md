# 开放平台

开放平台同时支持标准 OAuth2/OIDC 客户端和个人级 AppKey/AppSecret 调用。权限只能从 PageRegistry 中选择，不允许第三方应用声明系统未知权限。

## 管理入口

进入 **开放平台应用**：

- 第三方应用：注册 Public/Confidential 客户端、配置回调地址、权限和授权模式、轮换 Secret、删除应用。
- 我的 OpenAPI 凭证：创建个人 AppKey/AppSecret、设置权限、IP 白名单和过期时间、撤销凭证。

`ClientSecret` 和 `AppSecret` 只在创建或轮换成功时显示一次，后端只保存哈希或协议所需的安全材料。

## OAuth2 与 OIDC

内置 OpenIddict，支持：

| 模式 | 适用场景 |
| --- | --- |
| Authorization Code + PKCE | 浏览器、桌面端、移动端用户登录 |
| Refresh Token | 长会话续期 |
| Client Credentials | 无用户参与的可信服务调用 |

协议入口：

```text
/.well-known/openid-configuration
/connect/authorize
/connect/token
/connect/userinfo
/connect/introspect
/connect/revoke
```

生产环境应配置同一个 HTTPS 公网地址：

```text
MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ISSUER=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=false
```

首次启动会在持久化上传卷的 `.system` 目录生成 3072 位 RSA 签名证书。多实例部署必须共享该证书或显式挂载同一 PFX，否则不同实例签发的身份令牌无法互相验证。

## AppKey 签名

每个请求必须携带：

```text
X-MA-AppKey: ak_...
X-MA-Timestamp: Unix 秒时间戳
X-MA-Nonce: 8-128 位随机字符串
X-MA-Signature: HMAC-SHA256 十六进制签名
```

规范请求由六行组成：

```text
HTTP_METHOD
PATH
SORTED_AND_ESCAPED_QUERY
SHA256_BODY_HEX
TIMESTAMP
NONCE
```

使用 `AppSecret` 对上述 UTF-8 文本计算 HMAC-SHA256。服务端采用固定时间比较、默认 300 秒时间窗、Nonce 持久化防重放，并检查凭证状态、有效期、IP 白名单和权限集合。

## 安全边界

- 回调地址必须是 HTTPS，只有 loopback 本机地址允许 HTTP。
- 公共客户端不允许 Client Credentials。
- Secret 泄露后应立即轮换或撤销，旧令牌随应用删除而撤销。
- `MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=true` 只用于内网首次验收，公网部署必须关闭。
- 网关和 Nginx 必须保留 Host、`X-Forwarded-Proto` 与真实客户端 IP。
