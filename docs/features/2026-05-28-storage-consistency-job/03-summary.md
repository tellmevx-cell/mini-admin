# 文件存储一致性检查任务总结文档

## 本次完成

- 新增内置定时任务 `storage-consistency-check`。
- 初始化 seed 版本 `202605280004-storage-consistency-job`，用于给已有环境补任务。
- 扩展文件存储抽象，新增 `ExistsAsync`。
- 本地存储通过 `File.Exists` 检查文件是否存在。
- MinIO 存储通过 `HEAD` 请求检查对象是否存在。
- 定时任务执行器会扫描数据库文件记录并统计缺失数量、异常数量。
- 前端定时任务页面支持 `Warning` 状态的橙色展示和警告提示。

## 行为说明

任务只做检查，不做修复：

- 不删除数据库记录。
- 不删除真实存储文件。
- 不自动补偿缺失文件。
- 不扫描存储桶中没有数据库记录的对象。

执行结果：

- `Success`：所有数据库文件记录都能在实际存储中找到。
- `Warning`：存在文件缺失或检查异常。
- `Failed`：任务执行过程出现未处理异常。

## 核心实现

存储抽象：

```text
IFileStorageService.ExistsAsync(provider, path)
```

任务 Key：

```text
storage-consistency-check
```

默认任务配置：

```text
名称：检查文件存储一致性
间隔：86400 秒
启用：是
```

## 验证结果

红灯测试：

```text
测试失败，列表中只有 audit-log-cleanup，没有 storage-consistency-check。
```

过滤测试：

```text
dotnet test ... --filter "StorageConsistency"
已通过! - 失败: 0，通过: 2，已跳过: 0，总计: 2
```

完整后端测试：

```text
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
已通过! - 失败: 0，通过: 75，已跳过: 0，总计: 75
```

前端构建：

```text
pnpm run build:antd
Tasks: 11 successful, 11 total
```

## 如何手动测试

1. 重启后端，让 seed 生效。
2. 重新登录前端，进入 `系统监控 > 定时任务`。
3. 确认列表中存在 `检查文件存储一致性`。
4. 点击 `执行`。
5. 如果所有文件都存在，会提示成功。
6. 如果数据库记录对应的文件不存在，会提示警告。
7. 点击 `日志`，查看本次执行详情。

## 后续扩展

- 增加“孤儿文件扫描”，检查存储中存在但数据库不存在的对象。
- 增加一致性检查详情表，单独展示每个异常文件。
- 增加修复动作，例如标记异常、重新上传、删除无效记录。
- 增加分页扫描，支持大文件量场景。
