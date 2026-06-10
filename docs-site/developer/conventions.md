# 开发约定

约定的价值是让后续二开者少猜一点。MiniAdmin 新增功能时建议遵循本页规则。

## 命名约定

权限编码：

```text
module:resource:action
```

接口路径：

```text
/module/resource
```

前端 API 文件：

```text
src/api/<module>/<resource>.ts
```

前端页面：

```text
src/views/<module>/<resource>/index.vue
```

## 文档约定

完整功能放到：

```text
docs/features/YYYY-MM-DD-feature-name
```

固定包含：

- `01-requirements.md`
- `02-tasks.md`
- `03-summary.md`

运行手册放到：

```text
docs/runbooks
```

官网文档站放到：

```text
docs-site
```

## Git 约定

建议：

- 每个功能使用独立分支。
- 不把本地日志、构建产物和密钥提交。
- 合并前至少跑目标测试和类型检查。
- 不把无关格式化混进功能提交。

## 后端约定

- DTO 不直接复用实体。
- API 层不写复杂业务。
- 写操作要考虑权限、租户和审计。
- 外部服务放 Infrastructure。
- 可替换能力通过接口隔离。

## 前端约定

- 页面不直接散落请求 URL。
- 按钮权限和后端权限编码一致。
- 表格操作保持同一交互风格。
- 抽屉和弹窗关闭后不要造成重复跳转。
- 状态变更后刷新相关列表和详情。

## 验证约定

常用检查：

```powershell
git diff --check
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter YourFeatureTests
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

文档站检查：

```powershell
pnpm docs:build
```
