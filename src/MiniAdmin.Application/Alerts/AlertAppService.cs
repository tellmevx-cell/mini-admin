using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.SystemMonitor;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Application.Alerts;

public sealed class AlertAppService(
    IAlertRepository alertRepository,
    IAlertRuleRepository alertRuleRepository,
    ISystemMonitorAppService systemMonitorAppService,
    IUserNotificationAppService userNotificationAppService,
    IAlertNotificationRecipientRepository alertNotificationRecipientRepository,
    INotificationDeliveryService notificationDeliveryService) : IAlertAppService
{
    public Task<PageResult<AlertDto>> GetListAsync(
        AlertListQuery query,
        CancellationToken cancellationToken = default)
    {
        return alertRepository.GetListAsync(query, cancellationToken);
    }

    public Task<AlertDto?> AcknowledgeAsync(
        Guid id,
        string userName,
        AcknowledgeAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        return alertRepository.AcknowledgeAsync(
            id,
            userName,
            string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            cancellationToken);
    }

    public async Task<AlertScanResultDto> ScanAsync(CancellationToken cancellationToken = default)
    {
        var overview = await systemMonitorAppService.GetOverviewAsync(cancellationToken);
        var rules = await alertRuleRepository.GetEnabledAsync(cancellationToken);
        var signals = new List<AlertSignal>();

        foreach (var rule in rules)
        {
            AddRuleSignals(rule, overview, signals);
        }

        var notificationRuleCodes = rules
            .Where(rule => rule.NotifyEnabled || rule.EmailEnabled)
            .Select(rule => rule.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rulesByCode = rules.ToDictionary(rule => rule.Code, StringComparer.OrdinalIgnoreCase);

        var result = await alertRepository.SaveScanResultAsync(
            signals,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var notificationAlerts = result.CreatedAlerts
            .Where(alert => notificationRuleCodes.Contains(alert.Type))
            .ToArray();
        foreach (var alertGroup in notificationAlerts.GroupBy(alert => alert.Type))
        {
            if (!rulesByCode.TryGetValue(alertGroup.Key, out var rule))
            {
                continue;
            }

            var alerts = alertGroup.ToArray();
            var recipientUserIds = await alertNotificationRecipientRepository.ResolveUserIdsAsync(
                rule,
                cancellationToken);
            if (rule.NotifyEnabled)
            {
                await userNotificationAppService.CreateAlertNotificationsAsync(
                    recipientUserIds,
                    alerts,
                    cancellationToken);
            }

            if (rule.EmailEnabled)
            {
                await notificationDeliveryService.CreateAlertEmailDeliveriesAsync(
                    recipientUserIds,
                    alerts,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
        }

        return result;
    }

    private static void AddRuleSignals(
        AlertRuleDto rule,
        SystemMonitorOverviewDto overview,
        List<AlertSignal> signals)
    {
        switch (rule.Code)
        {
            case "MemoryHigh":
                if (overview.Memory.PhysicalMemoryUsedPercent >= (double)rule.Threshold)
                {
                    signals.Add(new AlertSignal(
                        rule.Code,
                        rule.Level,
                        rule.Name,
                        $"当前系统物理内存使用率 {overview.Memory.PhysicalMemoryUsedPercent}%，阈值 {rule.Threshold}%，已用 {FormatBytes(overview.Memory.UsedPhysicalMemoryBytes)}，总量 {FormatBytes(overview.Memory.TotalPhysicalMemoryBytes)}。",
                        "SystemMemory",
                        rule.NotifyEnabled));
                }

                break;

            case "DependencyUnhealthy":
                var unhealthyDependencies = overview.Dependencies
                    .Where(item => item.Status == "Unhealthy")
                    .ToArray();
                if (unhealthyDependencies.Length >= rule.Threshold)
                {
                    foreach (var dependency in unhealthyDependencies)
                    {
                        signals.Add(new AlertSignal(
                            rule.Code,
                            rule.Level,
                            $"{dependency.Name} 依赖异常",
                            dependency.Description,
                            dependency.Name,
                            rule.NotifyEnabled));
                    }
                }

                break;

            case "ScheduledJobFailed":
                if (overview.Recent.FailedScheduledJobCount >= rule.Threshold)
                {
                    signals.Add(new AlertSignal(
                        rule.Code,
                        rule.Level,
                        rule.Name,
                        $"近 {rule.WindowMinutes} 分钟失败定时任务数量：{overview.Recent.FailedScheduledJobCount}，阈值 {rule.Threshold}。",
                        "ScheduledJob",
                        rule.NotifyEnabled));
                }

                break;

            case "AuditFailureHigh":
                if (overview.Recent.FailedAuditLogCount >= rule.Threshold)
                {
                    signals.Add(new AlertSignal(
                        rule.Code,
                        rule.Level,
                        rule.Name,
                        $"近 {rule.WindowMinutes} 分钟失败操作日志数量：{overview.Recent.FailedAuditLogCount}，阈值 {rule.Threshold}。",
                        "AuditLog",
                        rule.NotifyEnabled));
                }

                break;

            case "AbnormalFileDetected":
                if (overview.Recent.AbnormalFileCount >= rule.Threshold)
                {
                    signals.Add(new AlertSignal(
                        rule.Code,
                        rule.Level,
                        rule.Name,
                        $"当前异常文件数量：{overview.Recent.AbnormalFileCount}，阈值 {rule.Threshold}。",
                        "ManagedFile",
                        rule.NotifyEnabled));
                }

                break;
        }
    }

    private static string FormatBytes(long value)
    {
        if (value >= 1024L * 1024 * 1024)
        {
            return $"{(double)value / 1024 / 1024 / 1024:F2} GB";
        }

        if (value >= 1024L * 1024)
        {
            return $"{(double)value / 1024 / 1024:F2} MB";
        }

        if (value >= 1024L)
        {
            return $"{(double)value / 1024:F2} KB";
        }

        return $"{value} B";
    }
}
