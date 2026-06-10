# 字典管理任务执行文档

> 回补整理。

## 任务清单

- [x] 定义字典类型和字典项实体
- [x] 实现字典类型 CRUD
- [x] 实现字典项 CRUD
- [x] 前端改为左右布局
- [x] 修复点击字典类型右侧不刷新的问题
- [x] 接入权限码控制

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/DictionaryType.cs`
- `src/MiniAdmin.Domain/Entities/DictionaryItem.cs`
- `src/MiniAdmin.Application.Contracts/Dictionaries/DictionaryTypeDto.cs`
- `src/MiniAdmin.Application.Contracts/Dictionaries/DictionaryItemDto.cs`
- `src/MiniAdmin.Application/Dictionaries/DictionaryAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfDictionaryRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/dictionary.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/dictionary/index.vue`

## 执行步骤

1. 创建字典类型和字典项实体。
2. Repository 支持按字典类型查询字典项。
3. API 暴露类型和字典项 CRUD。
4. 前端页面拆成左侧类型列表和右侧字典项表格。
5. 点击类型时更新选中类型并刷新右侧数据。
6. 删除类型前检查是否存在字典项。

## 当前状态

已完成。

