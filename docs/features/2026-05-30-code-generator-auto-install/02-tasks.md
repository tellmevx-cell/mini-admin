# 代码生成器第二阶段任务执行文档

## 任务拆分

- [x] 后端契约增加 `autoInstall` 开关。
- [x] 仓储层增加自动安装能力：建表、菜单权限 upsert、Admin 授权、缓存清理。
- [x] 生成服务在写文件成功后按开关执行自动安装。
- [x] 安装指引增加自动安装、菜单权限、后端重启状态。
- [x] 前端增加自动安装开关和生成成功提示。
- [x] 测试覆盖开启与关闭自动安装两种路径。
- [x] 更新总结文档。

## 执行命令

后端生成器测试：

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj -c Release --filter "CodeGenerator"
```

前端静态检查：

```powershell
npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue
```

前端构建：

```powershell
pnpm run build:antd
```

## 风险控制

- 自动建表只在目标表不存在时执行。
- 菜单权限用模块名生成确定性 GUID，和生成出来的 `MenuSeed` 保持一致。
- 文件冲突仍然阻止生成，不进入自动安装。
- 自动安装失败时记录失败历史并返回错误，避免用户误以为完整成功。
