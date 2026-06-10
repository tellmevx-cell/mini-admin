# 功能名称任务执行文档

## 任务清单

- [ ] 后端契约
- [ ] 后端实现
- [ ] 前端 API
- [ ] 前端页面
- [ ] 权限菜单
- [ ] 测试验证

## 涉及文件

### 后端

- `src/...`

### 前端

- `frontend/...`

### 测试

- `tests/...`

## 执行步骤

1. 编写失败测试
2. 实现最小后端能力
3. 补充前端页面
4. 跑后端测试
5. 跑前端构建
6. 更新总结文档

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

```powershell
pnpm run build:antd
```

## 当前状态

进行中。
