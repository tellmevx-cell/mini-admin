# 应用品牌与全局水印总结

## 交付内容

- 新增运行期品牌配置读取能力。
- 新增公开品牌配置接口，支持登录前读取。
- 前端启动时应用品牌名称，替换模板默认标题。
- 全局水印由系统参数控制，支持自定义文字。
- 系统参数初始化补充品牌与水印默认项。
- 参数设置保存 `app.brand.*` 或 `app.watermark.*` 后，前端会立即刷新运行时品牌配置。
- 全局水印已上移到应用根节点，登录后覆盖所有后台页面、弹窗和抽屉。
- 品牌读取兼容旧参数 `site_name`：当新的 `app.brand.name` 仍为默认值时，会使用已修改的旧站点名称。
- 新增独立数据种子版本 `202605310001-app-branding-watermark`，用于给已存在数据库补齐 `app.brand.*` 和 `app.watermark.*` 参数。

## 验证记录

- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --no-restore --filter AppBrandingAppServiceTests`：通过 3 个测试。
- `dotnet build MiniAdmin.slnx`：通过，0 个警告，0 个错误。
- `pnpm run build:antd`：通过，前端生产包构建成功。
- `npx impeccable --json ...`：返回 `[]`，未发现前端改动文件问题。
- `http://localhost:5320/health`：返回 `MiniAdmin.Api Healthy`。
- `http://localhost:5320/public/app-branding`：返回 `MiniAdmin` 默认品牌配置。
- `http://localhost:5666/`：前端开发服务返回 200。
- 重新启动前端 dev server 后，首页 HTML 标题已从 `Vben Admin Antd` 更新为 `MiniAdmin`。
- `GET /system/parameter/list?key=app.watermark`：返回 `app.watermark.enabled` 和 `app.watermark.text` 两条参数。

全量 `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --no-restore` 本次运行 124 秒超时，未拿到失败用例输出；本功能相关测试已单独通过。

## 管理方式

管理员进入 `系统管理 -> 参数设置`，搜索 `app.brand` 或 `app.watermark`，即可调整品牌与水印相关参数。

## 后续增强

- 可以新增专门的“外观设置”页面，把品牌名称、水印开关、水印文字做成更友好的表单。
- 可以扩展 logo、主题色、页脚版权等配置项。
