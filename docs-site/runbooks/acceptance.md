# 验收清单

完整验收手册见仓库内：

```text
docs/runbooks/workflow-message-center-acceptance-checklist.md
```

本页提供上线或交接前的高频检查项。

## 环境检查

- 后端 API 可启动。
- `/health/live` 返回成功且包含 `self` 检查。
- `/health/ready` 返回成功且包含 `database`、`primary-cache` 检查。
- 前端可打开。
- 管理员可登录。
- 文档站可构建。

## 平台基础检查

- 用户管理可打开。
- 角色管理可打开。
- 菜单管理可打开。
- 权限诊断可使用。
- 租户管理可打开。
- 审计日志可查询。
- 系统监控可打开。

## 平台内核检查

- `/scalar` 和 `/openapi/v1.json` 可访问，Dynamic API 的 OperationId 正确。
- 访问控制策略可新增、修改、启停和删除，显式 Deny 优先。
- 缓存管理可查询目录，并可按逻辑键或标签失效。
- PageRegistry 页面、路由、组件、权限和中英文标题同步正确。
- 标准 CRUD 不依赖重复 Controller、Endpoint 或 MenuSeed。

## 工作流检查

- 流程定义存在且启用。
- 发起审批成功。
- 我的申请能看到流程。
- 我的待办能看到任务。
- 同意成功。
- 驳回成功。
- 撤回成功。
- 转办成功。
- 催办能生成消息。
- 流程详情能展示表单、任务和流转记录。

## 抄送检查

- 抄送节点能生成抄送记录。
- 我的抄送能看到记录。
- 未读筛选有效。
- 打开详情后自动已读。
- 流程详情能看到抄送回执。

## 消息中心检查

- 顶部铃铛显示未读消息。
- 我的消息可筛选已读和未读。
- 工作流消息可跳转详情。
- 通知策略可保存。
- 订阅偏好可保存和恢复默认。
- 模板配置可保存并渲染。
- 投递记录能显示失败、跳过或成功状态。
- 在线聊天能加载联系人、历史消息、未读数和实时消息。

## 开放平台与网关检查

- OIDC 发现文档、授权端点和令牌端点可从 Web 公网入口访问。
- 第三方应用可创建、轮换 Secret、撤销和删除。
- 个人 OpenAPI 凭证只显示一次 Secret，HMAC 可校验且 Nonce 不能重放。
- 网关响应包含 `X-Trace-Id`，限流返回 429，熔断开启后可自动恢复。
- 灰度关闭时全部进入稳定目标；开启后百分比、白名单、租户和请求头规则有效。

## 基础设施检查

- Local、S3、OSS、COS、MinIO 配置可解析，未启用的提供方不影响启动。
- 系统监控可显示 CPU、内存、磁盘、网络、运行时，并对不可用硬件信息安全降级。
- 中文和英文菜单标题可按请求语言解析。
- 两个 worker 不能同时抢到同一个定时任务，租约过期后可接管。
- Outbox 失败会退避重试，超过上限进入 DeadLetter，手工重投有效。
- 生产弱密钥、InMemory 数据库或 Memory Cache 会阻止 API 启动。
- 备份目录包含数据库、上传卷、清单和通过校验的 `SHA256SUMS`。
- 在隔离环境至少完成一次 `restore-mini-admin.sh` 恢复演练。

## 自动化命令

生产 Docker Compose 只读验收：

```bash
cd /opt/mini-admin
bash scripts/acceptance-mini-admin.sh \
  --web-url https://admin.example.com
```

增加账号和备份闭环：

```bash
MINIADMIN_ACCEPTANCE_USERNAME=acceptance-admin \
MINIADMIN_ACCEPTANCE_PASSWORD='使用专用强密码' \
  bash scripts/acceptance-mini-admin.sh \
    --web-url https://admin.example.com \
    --with-login \
    --with-backup
```

该命令不包含恢复。恢复只能在隔离环境执行，不能把生产数据库当作演练目标。

后端完整测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj -c Release --no-restore
```

前端类型检查：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

前端生产构建：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd run build
```

文档站构建：

```powershell
pnpm docs:build
```

## 放行标准

可以放行：

- 核心手工验收通过。
- 后端关键测试通过。
- 前端类型检查通过。
- 文档站构建通过。
- Docker Compose 在目标 Linux 主机通过 `bash deploy.sh` 的完整健康检查。
- `acceptance-mini-admin.sh` 的只读检查与专用账号冒烟通过。
- 生产备份已同步到服务器之外，恢复演练记录可追踪。
- 没有高风险权限或数据隔离问题。

不建议放行：

- 登录失败。
- 菜单权限异常。
- 发起审批失败。
- 审批无法处理。
- 工作流消息完全不生成。
- 抄送已读状态不可信。
