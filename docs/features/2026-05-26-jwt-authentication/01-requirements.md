# JWT 登录认证需求文档

## 背景

后台系统需要标准登录认证能力，前端使用 token 调用后端接口。认证结果还需要和 Vben 的用户信息、权限码、动态菜单衔接。

## 目标

- 用户登录后返回 JWT access token。
- 受保护接口需要认证。
- token 中包含用户 ID、用户名、角色、安全戳。
- 支持后续密码修改后让旧 token 失效。

## 功能范围

- 登录接口。
- 登出接口。
- 用户信息接口。
- JWT Bearer 认证。
- 安全戳校验。

## 不做范围

- 不做刷新 token。
- 不做 OAuth2/OpenID Connect。

## 数据流转

```mermaid
flowchart LR
    A["POST /auth/login"] --> B["AuthAppService"]
    B --> C["校验用户名和密码"]
    C --> D["JwtTokenService 生成 Token"]
    D --> E["前端保存 accessToken"]
    E --> F["请求受保护接口"]
    F --> G["JwtBearer 验签和安全戳校验"]
```

## 验收标准

- [x] 登录成功返回 token。
- [x] 未登录访问受保护接口返回 401。
- [x] Vben 可用 token 获取用户信息和权限。
