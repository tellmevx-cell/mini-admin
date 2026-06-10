# Code Generator Existing Module Summary

## 完成内容

- `CodeGeneratorTableDto` 增加 `ExistingModule`，表列表和表详情都会返回已有模块摘要。
- 后端会扫描 `MiniAdminDbContext.cs` 和 `Persistence/Generated/*EntityTypeConfiguration.cs` 中的 `ToTable` 映射，识别已映射表。
- 代码生成器预览/生成仍然拦截已映射表，并在错误中说明具体实体名。
- 前端代码生成页面增加“已映射/可生成”状态，选择已映射表后显示已有模块卡片。
- 已映射表会禁用预览和生成，避免用户到最后一步才看到错误。

## 使用说明

如果选择 `mini_customer` 这类已有表，页面会提示它已经关联到 `Customer` 模块。此时应直接维护已有客户管理功能，或者换一张新的业务表生成新模块。

## 后续方向

下一阶段可以继续做“已有模块维护模式”：对比数据库字段和现有代码字段，给出新增字段、删除字段、需要人工处理的变更清单。
