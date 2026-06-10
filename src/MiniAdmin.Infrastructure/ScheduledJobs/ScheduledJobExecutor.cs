using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.ScheduledJobs;

public sealed class ScheduledJobExecutor(
    MiniAdminDbContext dbContext,
    IFileStorageService fileStorageService,
    IAlertAppService alertAppService,
    IWorkflowRepository workflowRepository,
    INotificationDeliveryService notificationDeliveryService) : IScheduledJobExecutor
{
    public async Task<ScheduledJobExecutionResult> ExecuteAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        return jobKey switch
        {
            "audit-log-cleanup" => await CleanupAuditLogsAsync(cancellationToken),
            "storage-consistency-check" => await CheckStorageConsistencyAsync(cancellationToken),
            "alert-scan" => await ScanAlertsAsync(cancellationToken),
            "workflow-sla-scan" => await ScanWorkflowSlaAsync(cancellationToken),
            "notification-delivery-retry" => await RetryNotificationDeliveriesAsync(cancellationToken),
            _ => new ScheduledJobExecutionResult("Failed", $"未知任务：{jobKey}")
        };
    }

    private async Task<ScheduledJobExecutionResult> ScanAlertsAsync(CancellationToken cancellationToken)
    {
        var result = await alertAppService.ScanAsync(cancellationToken);
        var status = result.ActiveSignalCount > 0 ? "Warning" : "Success";
        var message = result.ActiveSignalCount > 0
            ? $"发现 {result.ActiveSignalCount} 个活跃告警，新增 {result.CreatedCount} 个，更新 {result.UpdatedCount} 个，恢复 {result.RecoveredCount} 个"
            : $"未发现活跃告警，恢复 {result.RecoveredCount} 个告警";

        return new ScheduledJobExecutionResult(status, message);
    }

    private async Task<ScheduledJobExecutionResult> ScanWorkflowSlaAsync(CancellationToken cancellationToken)
    {
        var result = await workflowRepository.ScanOverdueTasksAsync(DateTimeOffset.UtcNow, cancellationToken);
        var status = result.RemindedTaskCount > 0 ? "Warning" : "Success";
        var message = result.OverdueTaskCount > 0
            ? $"发现 {result.OverdueTaskCount} 个超时待办，自动催办 {result.RemindedTaskCount} 个"
            : "未发现超时待办";
        var details = result.Details.Select(detail => new ScheduledJobExecutionDetail(
            "WorkflowOverdue",
            "WorkflowTask",
            detail.TaskId,
            detail.InstanceTitle,
            detail.ApproverUserName,
            detail.NodeName,
            "Warning",
            $"截止时间：{detail.DueAt:yyyy-MM-dd HH:mm}")).ToArray();

        return new ScheduledJobExecutionResult(status, message, details);
    }

    private async Task<ScheduledJobExecutionResult> RetryNotificationDeliveriesAsync(CancellationToken cancellationToken)
    {
        var result = await notificationDeliveryService.RetryFailedAsync(
            maxRetryCount: 3,
            batchSize: 50,
            cancellationToken);
        if (result.RetriedCount == 0)
        {
            return new ScheduledJobExecutionResult("Success", "没有需要自动重试的通知投递记录");
        }

        var status = result.FailedCount > 0 || result.SkippedCount > 0 ? "Warning" : "Success";
        var details = result.Items.Select(delivery =>
        {
            var detailStatus = delivery.Status == "Succeeded" ? "Success" : "Warning";
            return new ScheduledJobExecutionDetail(
                "NotificationDeliveryRetry",
                "NotificationDelivery",
                delivery.Id.ToString(),
                delivery.Title,
                delivery.Channel,
                $"{delivery.SourceType}/{delivery.SourceId}",
                detailStatus,
                delivery.ErrorMessage ?? $"重试结果：{delivery.Status}");
        }).ToArray();

        return new ScheduledJobExecutionResult(
            status,
            $"自动重试 {result.RetriedCount} 条通知投递记录，成功 {result.SucceededCount} 条，失败 {result.FailedCount} 条，跳过 {result.SkippedCount} 条",
            details);
    }

    private async Task<ScheduledJobExecutionResult> CleanupAuditLogsAsync(CancellationToken cancellationToken)
    {
        var retentionBoundary = DateTimeOffset.UtcNow.AddDays(-90);
        int deletedCount;
        if (dbContext.Database.IsRelational())
        {
            deletedCount = await dbContext.AuditLogs
                .Where(log => log.CreatedAt < retentionBoundary)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var expiredLogs = await dbContext.AuditLogs
                .Where(log => log.CreatedAt < retentionBoundary)
                .ToArrayAsync(cancellationToken);
            dbContext.AuditLogs.RemoveRange(expiredLogs);
            deletedCount = expiredLogs.Length;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ScheduledJobExecutionResult("Success", $"已清理 {deletedCount} 条 90 天前审计日志");
    }

    private async Task<ScheduledJobExecutionResult> CheckStorageConsistencyAsync(CancellationToken cancellationToken)
    {
        var files = await dbContext.ManagedFiles
            .OrderBy(file => file.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var missingCount = 0;
        var errorCount = 0;
        var examples = new List<string>();
        var details = new List<ScheduledJobExecutionDetail>();
        foreach (var file in files)
        {
            try
            {
                var exists = await fileStorageService.ExistsAsync(
                    file.StorageProvider,
                    file.StoragePath,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                missingCount++;
                file.Status = "Missing";
                AddExample(examples, $"{file.OriginalName} 文件不存在");
                details.Add(new ScheduledJobExecutionDetail(
                    "StorageMissing",
                    "ManagedFile",
                    file.Id.ToString(),
                    file.OriginalName,
                    file.StorageProvider,
                    file.StoragePath,
                    "Warning",
                    "文件不存在"));
            }
            catch (Exception exception)
            {
                errorCount++;
                AddExample(examples, $"{file.OriginalName} 检查异常：{exception.Message}");
                details.Add(new ScheduledJobExecutionDetail(
                    "StorageCheckError",
                    "ManagedFile",
                    file.Id.ToString(),
                    file.OriginalName,
                    file.StorageProvider,
                    file.StoragePath,
                    "Warning",
                    exception.Message));
            }
        }

        if (missingCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (missingCount == 0 && errorCount == 0)
        {
            return new ScheduledJobExecutionResult("Success", $"已检查 {files.Length} 个文件，未发现异常");
        }

        var exampleMessage = examples.Count == 0
            ? string.Empty
            : $"。示例：{string.Join("；", examples)}";

        return new ScheduledJobExecutionResult(
            "Warning",
            $"已检查 {files.Length} 个文件，缺失 {missingCount} 个，异常 {errorCount} 个{exampleMessage}",
            details);
    }

    private static void AddExample(List<string> examples, string example)
    {
        if (examples.Count < 3)
        {
            examples.Add(example);
        }
    }
}
