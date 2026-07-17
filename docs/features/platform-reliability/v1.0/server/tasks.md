# 生产可靠性底座 - 服务端任务清单

全局约束：不改变现有业务 API；不引入消息中间件；所有抢占由数据库条件更新保证，不能使用进程内锁冒充分布式锁。

## 执行状态

1. ✅ 已完成 - 数据模型、迁移和配置契约
2. ✅ 已完成 - 定时任务租约、心跳与安全完成
3. ✅ 已完成 - 事务 Outbox、Inbox 和后台投递
4. ✅ 已完成 - 运维查询与死信重投接口
5. ✅ 已完成 - liveness/readiness、生产配置门禁和迁移互斥
6. ✅ 已完成 - 备份、恢复和保留策略脚本
7. ✅ 已完成 - 文档、全量测试和本机构建验收
8. ✅ 已完成 - 自动化服务器验收脚本与 CI 发布门禁
9. ⏳ 待目标服务器执行 - Docker Compose 展开与真实恢复演练

## 数据模型与迁移 `✅ 已完成`

- ✅ 定时任务增加 lease token、owner、expiry 和 heartbeat 字段。
- ✅ 新建 Outbox/Inbox 实体、索引和迁移。
- ✅ 同步 EF Core 模型快照，并验证可生成幂等迁移 SQL。

## 定时任务可靠执行 `✅ 已完成`

- ✅ 使用条件更新抢占任务。
- ✅ 按“抢一条、立即执行、再抢下一条”避免排队租约过期。
- ✅ 执行期间续租，完成时按 token 写日志并释放。
- ✅ 停机或租约丢失时禁止旧实例覆盖新实例状态。

## Outbox/Inbox `✅ 已完成`

- ✅ 可靠事件与业务数据同事务写入。
- ✅ 批次消息立即并行启动心跳，避免等待期间租约过期。
- ✅ 消费时恢复事件的租户上下文。
- ✅ 处理器数据库结果与 Inbox 回执同事务提交。
- ✅ 支持指数退避、死信、超时接管、手工重投和优雅停机。

## 生产运行保护 `✅ 已完成`

- ✅ 区分 `/health/live` 与 `/health/ready`。
- ✅ readiness 检查数据库和 Redis 主缓存真实读写。
- ✅ Production 阻止弱密钥、占位密钥、InMemory 数据库和 Memory Cache。
- ✅ 多实例数据库初始化使用 MySQL advisory lock。
- ✅ Docker 停机宽限期覆盖后台 worker 收尾。

## 灾备脚本 `✅ 已完成`

- ✅ 一致性备份 MySQL、上传卷、环境快照和部署清单。
- ✅ 生成并验证 SHA256，采用临时目录后原子发布备份。
- ✅ 包含上传卷时短暂停止 API，并在成功或异常退出时自动拉起。
- ✅ 恢复前自动安全备份并要求 `--confirm`。
- ✅ 恢复后清 Redis、启动全栈并等待健康。
- ✅ `backups/` 和临时 `.env` 备份已加入 Git 忽略。

## 本地验收 `✅ 已完成`

- ✅ 后端全量测试：280/280。
- ✅ 可靠性与生产配置专项测试通过。
- ✅ 解决方案构建：0 warning / 0 error。
- ✅ 前端 typecheck 与生产构建通过。
- ✅ 文档站构建通过。
- ✅ 备份/恢复脚本 `bash -n` 与 `--help` 通过。
- ✅ EF Core 迁移列表和幂等 SQL 生成通过。
- ✅ `git diff --check` 通过。

## 自动化发布门禁 `✅ 已完成`

- ✅ `acceptance-mini-admin.sh` 检查环境密钥、Compose、容器、完整代理链路、OIDC 和 TraceId。
- ✅ 账号登录与备份采用显式开关，默认验收保持只读。
- ✅ CI 对部署、备份、恢复、验收脚本执行 Shell 语法检查。
- ✅ CI 展开 Docker Compose 并核对完整服务清单。
- ✅ CI 生成 MiniAdmin 与 OpenPlatform 两套幂等迁移 SQL 并上传构件。
- ✅ 合并到 `main` 后由 CI 完整构建 API、Gateway、Web 三个部署镜像。

## 目标服务器验收 `⏳ 上线前必须执行`

- ⬜ `docker compose --env-file .env config --quiet`。
- ⬜ `bash deploy.sh` 后所有容器 healthy。
- ⬜ `bash scripts/acceptance-mini-admin.sh --web-url <正式入口>` 通过。
- ⬜ `/health/live` 与 `/health/ready` 均返回 Healthy。
- ⬜ 创建正式备份并复制到隔离目录。
- ⬜ 在隔离环境运行 `restore-mini-admin.sh <backup> --confirm`。
- ⬜ 恢复后验证登录、租户、权限、文件下载、工作流和 Outbox。
