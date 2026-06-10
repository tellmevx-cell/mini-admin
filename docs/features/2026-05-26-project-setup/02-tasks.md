# 项目初始化与分层任务执行文档

## 任务清单

- [x] 创建解决方案
- [x] 创建后端项目
- [x] 设置目标框架
- [x] 建立项目引用
- [x] 编写学习文档

## 涉及文件

- `MiniAdmin.slnx`
- `src/MiniAdmin.Api/MiniAdmin.Api.csproj`
- `src/MiniAdmin.Application/MiniAdmin.Application.csproj`
- `src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj`
- `src/MiniAdmin.Domain/MiniAdmin.Domain.csproj`
- `src/MiniAdmin.Domain.Shared/MiniAdmin.Domain.Shared.csproj`
- `src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj`
- `src/MiniAdmin.Shared/MiniAdmin.Shared.csproj`
- `docs/01-project-setup.md`

## 执行步骤

1. 使用 `dotnet new` 创建解决方案和项目。
2. 按分层规则建立引用。
3. 确认项目目标框架。
4. 写入 `docs/01-project-setup.md`。

## 验证命令

```powershell
dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx
```

## 当前状态

已完成，本文档为回补整理。
