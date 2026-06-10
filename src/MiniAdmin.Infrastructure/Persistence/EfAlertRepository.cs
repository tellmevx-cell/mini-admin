using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfAlertRepository(MiniAdminDbContext dbContext) : IAlertRepository
{
    public async Task<PageResult<AlertDto>> GetListAsync(
        AlertListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var alertsQuery = dbContext.Alerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            alertsQuery = alertsQuery.Where(alert => alert.Type == query.Type);
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            alertsQuery = alertsQuery.Where(alert => alert.Level == query.Level);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            alertsQuery = alertsQuery.Where(alert => alert.Status == query.Status);
        }

        var total = await alertsQuery.CountAsync(cancellationToken);
        var items = await alertsQuery
            .OrderBy(alert => alert.Status == "Recovered" ? 1 : 0)
            .ThenByDescending(alert => alert.LastTriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(alert => ToDto(alert))
            .ToArrayAsync(cancellationToken);

        return new PageResult<AlertDto>(items, total);
    }

    public async Task<AlertDto?> AcknowledgeAsync(
        Guid id,
        string userName,
        string? remark,
        CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.Alerts.SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        if (alert.Status != "Recovered")
        {
            alert.Status = "Acknowledged";
        }

        alert.AcknowledgedBy = userName;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.AcknowledgeRemark = remark;
        alert.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(alert);
    }

    public async Task<AlertScanResultDto> SaveScanResultAsync(
        IReadOnlyList<AlertSignal> signals,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var created = 0;
        var updated = 0;
        var recovered = 0;
        var createdAlerts = new List<Alert>();
        var activeKeys = signals
            .Select(signal => CreateKey(signal.Type, signal.Source))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unresolvedAlerts = await dbContext.Alerts
            .Where(alert => alert.Status != "Recovered")
            .ToArrayAsync(cancellationToken);

        foreach (var signal in signals)
        {
            var alert = unresolvedAlerts.SingleOrDefault(existing =>
                existing.Type == signal.Type &&
                existing.Source == signal.Source);

            if (alert is null)
            {
                var createdAlert = new Alert
                {
                    Id = Guid.NewGuid(),
                    Type = signal.Type,
                    Level = signal.Level,
                    Title = signal.Title,
                    Content = signal.Content,
                    Source = signal.Source,
                    Status = "Active",
                    FirstTriggeredAt = now,
                    LastTriggeredAt = now,
                    TriggerCount = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Alerts.Add(createdAlert);
                createdAlerts.Add(createdAlert);
                created++;
                continue;
            }

            alert.Level = signal.Level;
            alert.Title = signal.Title;
            alert.Content = signal.Content;
            alert.LastTriggeredAt = now;
            alert.TriggerCount++;
            alert.UpdatedAt = now;
            updated++;
        }

        foreach (var alert in unresolvedAlerts)
        {
            if (activeKeys.Contains(CreateKey(alert.Type, alert.Source)))
            {
                continue;
            }

            alert.Status = "Recovered";
            alert.RecoveredAt = now;
            alert.UpdatedAt = now;
            recovered++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AlertScanResultDto(
            signals.Count,
            created,
            updated,
            recovered,
            createdAlerts.Select(ToDto).ToArray());
    }

    private static string CreateKey(string type, string source)
    {
        return $"{type}:{source}";
    }

    private static AlertDto ToDto(Alert alert)
    {
        return new AlertDto(
            alert.Id.ToString(),
            alert.Type,
            alert.Level,
            alert.Title,
            alert.Content,
            alert.Source,
            alert.Status,
            alert.FirstTriggeredAt,
            alert.LastTriggeredAt,
            alert.RecoveredAt,
            alert.AcknowledgedBy,
            alert.AcknowledgedAt,
            alert.AcknowledgeRemark,
            alert.TriggerCount);
    }
}
