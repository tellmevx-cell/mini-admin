# 全后台视觉一致性整理执行

## 方案

新增 `src/styles/mini-admin-polish.css`，在 `bootstrap.ts` 中全局引入。

选择全局样式层的原因：

- 大量后台页面都复用 `table-shell`、`table-toolbar`、`panel`、`info-panel` 等结构。
- 全局层可以一次性覆盖常规列表页、租户页、业务生成页和工作台页。
- 避免逐页重复修改 scoped style，降低后续维护成本。

## 已整理内容

- 统一常见面板容器的边框、圆角和背景：
  - `table-shell`
  - `user-panel`
  - `department-panel`
  - `panel`
  - `info-panel`
  - `summary-panel`
  - 项目运行管理相关容器
- 统一工具栏和标题层级：
  - `table-toolbar`
  - `toolbar`
  - `panel-header`
  - `panel-heading`
  - `panel-title`
- 统一 Ant Design 表格细节：
  - 表头背景和字重
  - 行 hover 状态
  - 分页间距
  - 单元格长文本换行
- 统一弹窗和抽屉基础质感：
  - 标题字重
  - 表单标签颜色和字重
  - 输入控件圆角
- 增加移动端工具栏折行规则，避免按钮和筛选输入互相挤压。

## 不变内容

- 不改接口。
- 不改业务逻辑。
- 不改按钮权限。
- 不改路由和菜单。
- 不改已有页面的数据结构和交互流程。
