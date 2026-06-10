# 代码生成器字段选择任务执行文档

## 任务清单

- [x] 增加前端字段选择状态模型。
- [x] 选择表后按系统字段规则初始化勾选状态。
- [x] 新增表字段选择区表格。
- [x] 实现字段勾选/取消勾选同步字段配置区。
- [x] 实现全选/取消全选当前可生成字段。
- [x] 调整预览请求，只提交已勾选字段和自定义字段。
- [x] 保留自定义字段新增/删除能力。
- [x] 优化字段配置区文案和空状态。
- [x] 修复数据表行点击选择。
- [x] 顶部表名输入改为可搜索下拉选择。
- [x] 运行前端构建。
- [x] 编写 `03-summary.md`。

## 涉及文件

- `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`
- `docs/features/2026-05-29-code-generator-field-selection/01-requirements.md`
- `docs/features/2026-05-29-code-generator-field-selection/02-tasks.md`
- `docs/features/2026-05-29-code-generator-field-selection/03-summary.md`

## 实现要点

### 字段状态

前端维护两类字段：

- `tableFieldSelections`：数据库表字段选择状态。
- `fields`：真正进入生成请求的字段配置。

### 系统字段判断

字段名统一转小写后判断。

默认不勾选：

- 主键字段。
- `id`、`tenant_id`、`created_at`、`updated_at`、`deleted_at`。
- `create_time`、`update_time`、`create_by`、`update_by`。
- `is_deleted`。

### 同步策略

- 勾选字段：如果 `fields` 中不存在，则根据列信息生成默认配置并追加。
- 取消勾选：从 `fields` 中移除该数据库字段。
- 自定义字段：不依赖数据库列选择，保留独立删除。

## 验证命令

```powershell
pnpm run build:antd
```

## 当前状态

已完成第一版两段式字段选择。后续可继续增强字段排序、字段配置缓存和字典字段智能识别。
