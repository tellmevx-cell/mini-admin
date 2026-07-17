# 生产可靠性运行手册

本手册适用于 MiniAdmin 的标准 Docker Compose / 1Panel 生产部署，覆盖上线门禁、健康检查、多实例后台任务、可靠事件、备份恢复和故障处置。

## 上线原则

- 公网只暴露 Web 或反向代理入口，API、Gateway、MySQL、Redis 保持内网或回环地址。
- 生产必须使用 MySQL 和 Redis，不允许 InMemory 数据库或 Memory Cache。
- `.env`、备份目录、OIDC 证书和上传卷都属于敏感资产，不能提交 Git。
- 发布前先备份；数据库结构变更必须保持至少一个版本的前后兼容。
- 定时任务和可靠事件处理器都按“可能重试”设计，不依赖进程内锁。

## 生产启动门禁

API 在 `Production` 环境启动时会拒绝以下配置：

- JWT 密钥少于 32 字符、使用默认值或仍含占位符。
- 开放平台签名、加密或 OpenAPI 凭证密钥缺失、过短或仍含占位符。
- `Database:Provider` 不是 `MySql`。
- 数据库连接串为空或包含占位符。
- `Cache:Provider` 不是 `Redis`，或 Redis 配置为空。

首次执行 `bash deploy.sh` 会生成独立的 JWT、开放平台签名、开放平台加密和 OpenAPI 凭证加密密钥。不要在已有数据后直接重新生成这些密钥，否则会导致现有令牌失效或加密凭证无法解密。

存量部署如果曾让开放平台密钥沿用 JWT，验收脚本会给出警告但不会中断发布。轮换时必须遵循以下顺序：

1. 先完整备份 `.env` 和数据库，并安排维护窗口。
2. 开放平台签名、令牌加密密钥可分别轮换；轮换后要求第三方应用重新登录并获取令牌。
3. `MINIADMIN_OPENAPI_CREDENTIAL_ENCRYPTION_KEY` 不能直接更换，否则已有个人 OpenAPI 凭证将无法解密。先撤销并重新签发全部凭证，确认调用方已切换后再轮换。
4. 每次只更换一个密钥，重启并运行 `acceptance-mini-admin.sh --with-login`，确认后再处理下一个。

不要使用 `deploy.sh --force-env` 给已有数据库整体重建 `.env`。

多 API 实例同时启动时，MySQL `GET_LOCK` 会串行化迁移和种子初始化。等待上限由 `MINIADMIN_DB_INIT_LOCK_TIMEOUT_SECONDS` 控制，默认 180 秒。

## 健康检查

| 地址 | 含义 | 失败时处理 |
| --- | --- | --- |
| `/health/live` | 进程仍在运行 | 容器或进程可重启 |
| `/health/ready` | 数据库可连接且 Redis 主缓存可读写 | 摘流并检查依赖，不要盲目重启循环 |
| `/health` | 兼容入口，等同 readiness | 用于人工和完整链路检查 |

```bash
curl -fsS http://127.0.0.1:8080/health/live
curl -fsS http://127.0.0.1:8080/health/ready
curl -fsS http://127.0.0.1:8088/api/health
curl -fsS http://127.0.0.1:5666/api/health
```

Docker 使用 liveness 判断 API 进程，避免 Redis 短暂抖动触发无意义重启。负载均衡、发布脚本和监控告警应使用 readiness。

## 自动化上线验收

部署完成后先执行只读验收。脚本会检查生产密钥、端口暴露、Compose 配置、五个容器状态、Web、Gateway、liveness、readiness、OIDC 发现文档和 `X-Trace-Id`：

```bash
cd /opt/mini-admin
bash scripts/acceptance-mini-admin.sh
```

如果使用公网域名或反向代理，应同时验证真实入口：

```bash
bash scripts/acceptance-mini-admin.sh \
  --web-url https://admin.example.com
```

需要验证真实账号链路时，通过环境变量传递验收账号，避免把密码写进脚本或命令历史：

```bash
MINIADMIN_ACCEPTANCE_USERNAME=acceptance-admin \
MINIADMIN_ACCEPTANCE_PASSWORD='使用专用强密码' \
  bash scripts/acceptance-mini-admin.sh \
    --web-url https://admin.example.com \
    --with-login
```

`--with-login` 会依次执行登录、`/user/info` 和退出。仅在生产确实开启 OpenAPI 文档时增加 `--check-openapi`。需要在验收后生成正式备份时增加 `--with-backup`，该选项会按一致性快照规则短暂停止 API；普通验收不会修改数据库或容器配置。

验收脚本不会自动执行恢复。恢复会覆盖数据，只能在隔离环境按“验证恢复”章节单独演练。

## 定时任务多实例规则

- 到期任务通过数据库条件更新抢占，只有一个实例能获得 lease token。
- worker 按“抢一条、立即执行、再抢下一条”工作，未开始的任务不会持有空闲租约。
- 长任务每 30 秒续租；租约丢失后旧实例不能覆盖新实例状态。
- 实例异常退出后，其他实例会在默认 120 秒租约过期后接管。

租约能避免正常情况下的并发重复，但无法回滚已经发出的外部副作用。自定义 `IScheduledJobExecutor` 动作仍必须幂等，并尊重取消令牌。

## Outbox/Inbox 运行规则

可靠事件与业务数据同事务写入 `mini_outbox_messages`。worker 并行处理已抢占批次并立即为每条消息启动心跳；租户事件会恢复创建时的 `TenantId`。

检查异常消息：

```bash
curl -H "Authorization: Bearer <token>" \
  "http://127.0.0.1:8080/system/outbox-message/list?status=DeadLetter&page=1&pageSize=20"
```

处理死信的顺序：

1. 阅读 `LastError`，确认是代码、配置还是外部依赖问题。
2. 修复并发布处理器，确认新实例 readiness 正常。
3. 调用 `POST /system/outbox-message/{id}/retry` 手工重投。
4. 观察消息进入 `Succeeded`，并核对下游幂等结果。

不要通过直接改表无限重试。事件类型或处理器类型改名之前，先清理或迁移存量消息。

## 一键备份

标准备份包含 MySQL、上传卷、部署清单、环境快照和 SHA256：

```bash
cd /opt/mini-admin
bash scripts/backup-mini-admin.sh
```

默认输出到 `backups/<UTC时间>/`。为了让数据库与上传卷一致，脚本在包含上传文件时会短暂停止 API；无论成功或失败都会自动拉起并等待健康。只备份数据库时不会停止 API：

```bash
bash scripts/backup-mini-admin.sh --skip-uploads
```

自定义目录与保留天数：

```bash
bash scripts/backup-mini-admin.sh \
  --output /data/mini-admin-backups \
  --retention-days 30
```

每日定时任务示例：

```text
0 3 * * * cd /opt/mini-admin && /usr/bin/bash scripts/backup-mini-admin.sh --output /data/mini-admin-backups --retention-days 30 >> /var/log/mini-admin-backup.log 2>&1
```

本机备份不能应对整台服务器或磁盘损坏。至少再将完整备份目录加密同步到另一台服务器或对象存储，并定期检查同步结果。

## 验证恢复

恢复会覆盖当前数据库和上传卷，必须显式确认：

```bash
cd /opt/mini-admin
bash scripts/restore-mini-admin.sh \
  /data/mini-admin-backups/20260717T030000Z \
  --confirm
```

脚本会：

1. 校验 `SHA256SUMS`、gzip 和备份格式。
2. 确认 MySQL、Redis 可用。
3. 默认先创建当前系统的安全备份。
4. 停止写服务，重建并导入数据库。
5. 恢复上传卷和原有加密密钥。
6. 清空 Redis，避免授权、菜单或字典读取旧缓存。
7. 启动全部服务并等待健康。

仅在全新灾备主机、确认没有旧数据时使用 `--skip-safety-backup`。如果明确只恢复数据库，可增加 `--skip-uploads`。使用 `--keep-current-security` 会保留当前密钥，只有确认备份数据库不含依赖旧密钥的加密数据时才能使用。

每月至少在隔离环境演练一次恢复。没有演练过的备份不能视为可用备份。

## 发布与回滚

发布前：

```bash
bash scripts/backup-mini-admin.sh --output /data/mini-admin-backups --retention-days 30
bash deploy.sh
bash scripts/acceptance-mini-admin.sh --web-url https://admin.example.com
```

数据库迁移应采用“先扩展、后切换、再清理”：先添加兼容字段和表，确认所有实例升级后再删除旧结构。应用回滚不自动回滚数据库迁移；需要回滚数据时使用经过验证的备份。

## 故障排查

```bash
docker compose ps
docker compose logs --tail=300 api mysql redis gateway
curl -v http://127.0.0.1:8080/health/live
curl -v http://127.0.0.1:8080/health/ready
df -h
free -h
```

| 现象 | 优先检查 |
| --- | --- |
| live 失败 | 进程崩溃、OOM、端口、启动配置门禁 |
| live 正常、ready 失败 | MySQL、Redis、连接数、DNS、网络 |
| API 启动等待超时 | 其他实例迁移、数据库锁、慢迁移 |
| 定时任务长期不运行 | 是否启用、`NextRunAt`、租约 owner/expiry、任务日志 |
| Outbox 大量 Retry | 处理器异常、外部依赖、数据库连接 |
| Outbox DeadLetter | 修复根因后按消息手工重投 |
| 恢复后权限异常 | Redis 是否已清空、租户和角色数据是否来自同一备份 |

生产验收还应执行[验收清单](./acceptance.md)。
