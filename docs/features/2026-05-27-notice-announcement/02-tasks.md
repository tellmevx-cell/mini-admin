# 通知公告任务执行文档

> 回补整理。

## 任务清单

- [x] 定义通知公告实体
- [x] 新增通知 DTO 和保存请求
- [x] 实现通知分页和 CRUD
- [x] 前端通知公告页面
- [x] 种子菜单和权限码

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Notice.cs`
- `src/MiniAdmin.Application.Contracts/Notices/NoticeDto.cs`
- `src/MiniAdmin.Application.Contracts/Notices/NoticeListQuery.cs`
- `src/MiniAdmin.Application.Contracts/Notices/SaveNoticeRequest.cs`
- `src/MiniAdmin.Application/Notices/NoticeAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfNoticeRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/notice.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/notice/index.vue`

## 执行步骤

1. 创建通知公告实体。
2. 实现通知公告分页查询。
3. 实现通知公告新增、编辑、删除。
4. API 增加 `/system/notice` 系列接口。
5. 前端增加通知公告页面。
6. 增加菜单和按钮权限种子。

## 当前状态

已完成。

