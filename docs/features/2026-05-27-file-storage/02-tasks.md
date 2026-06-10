# 文件存储任务执行文档

> 回补整理。

## 任务清单

- [x] 定义文件元数据实体
- [x] 定义文件存储抽象
- [x] 实现本地存储
- [x] 实现 MinIO 存储
- [x] 实现文件上传、下载、删除接口
- [x] 前端文件管理页面
- [x] 修复上传失败问题
- [x] 验证 MySQL 元数据写入

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/ManagedFile.cs`
- `src/MiniAdmin.Application.Contracts/Files/IFileStorageService.cs`
- `src/MiniAdmin.Application.Contracts/Files/FileDto.cs`
- `src/MiniAdmin.Application/Files/FileAppService.cs`
- `src/MiniAdmin.Infrastructure/Files/LocalFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/Files/MinioFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfFileRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/file.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/file/index.vue`

## 执行步骤

1. 新增 `ManagedFile` 元数据实体。
2. 定义 `IFileStorageService` 统一本地和 MinIO 行为。
3. 实现本地文件写入、读取、删除。
4. 实现 MinIO 上传、下载、删除。
5. 上传完成后写入文件元数据。
6. 下载时根据元数据读取真实文件流。
7. 前端增加上传、下载、删除操作。
8. 修复上传接口表单字段和请求处理问题。

## 当前状态

已完成。

