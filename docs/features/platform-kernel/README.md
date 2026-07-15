# 平台内核

平台内核负责 MiniAdmin 中跨业务域复用且边界稳定的能力：动态 API、页面元数据、RBAC + ABAC、分布式缓存、请求上下文、开放平台、实时通信和基础设施适配。

本模块借鉴 [XiHan.BasicApp](https://gitee.com/XiHanFun/XiHan.BasicApp) 的模块化与单一事实源思路，但不会复制其框架实现。MiniAdmin 保留现有分层与 API 兼容性，通过独立内核逐步迁移，避免一次性重写业务模块。

## 版本

- [v1.0 分析](v1.0/analysis.md)
- [v1.0 服务端设计](v1.0/server/design.md)
- [v1.0 服务端任务](v1.0/server/tasks.md)
