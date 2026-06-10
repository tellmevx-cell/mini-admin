using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfAlertRuleRepository(MiniAdminDbContext dbContext) : IAlertRuleRepository
{
    public async Task<PageResult<AlertRuleDto>> GetListAsync(
        AlertRuleListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var rulesQuery = dbContext.AlertRules
            .Include(rule => rule.Recipients)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            rulesQuery = rulesQuery.Where(rule =>
                rule.Code.Contains(keyword) ||
                rule.Name.Contains(keyword) ||
                rule.Description.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            rulesQuery = rulesQuery.Where(rule => rule.Level == query.Level);
        }

        if (query.Enabled.HasValue)
        {
            rulesQuery = rulesQuery.Where(rule => rule.Enabled == query.Enabled.Value);
        }

        var total = await rulesQuery.CountAsync(cancellationToken);
        var rules = await rulesQuery
            .OrderBy(rule => rule.Sort)
            .ThenBy(rule => rule.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var items = await ToDtosAsync(rules, cancellationToken);

        return new PageResult<AlertRuleDto>(items, total);
    }

    public async Task<IReadOnlyList<AlertRuleDto>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.AlertRules
            .Include(rule => rule.Recipients)
            .AsNoTracking()
            .Where(rule => rule.Enabled)
            .OrderBy(rule => rule.Sort)
            .ThenBy(rule => rule.Code)
            .ToArrayAsync(cancellationToken);
        return await ToDtosAsync(rules, cancellationToken);
    }

    public async Task<AlertRuleDto?> UpdateAsync(
        Guid id,
        UpdateAlertRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Threshold), "Alert rule threshold cannot be negative.");
        }

        if (request.WindowMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.WindowMinutes), "Alert rule window must be at least 1 minute.");
        }

        var rule = await dbContext.AlertRules
            .SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken);
        if (rule is null)
        {
            return null;
        }

        rule.Level = NormalizeLevel(request.Level);
        rule.Threshold = request.Threshold;
        rule.WindowMinutes = request.WindowMinutes;
        rule.Enabled = request.Enabled;
        rule.NotifyEnabled = request.NotifyEnabled;
        rule.EmailEnabled = request.EmailEnabled;
        rule.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        var existingRecipients = await dbContext.AlertRuleRecipients
            .Where(recipient => recipient.AlertRuleId == rule.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.AlertRuleRecipients.RemoveRange(existingRecipients);

        var roleIds = (request.RecipientRoleIds ?? Array.Empty<Guid>())
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        foreach (var roleId in roleIds)
        {
            dbContext.AlertRuleRecipients.Add(new AlertRuleRecipient
            {
                Id = Guid.NewGuid(),
                AlertRuleId = rule.Id,
                RecipientType = "Role",
                RecipientId = roleId,
                CreatedAt = now
            });
        }

        var userIds = (request.RecipientUserIds ?? Array.Empty<Guid>())
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        foreach (var userId in userIds)
        {
            dbContext.AlertRuleRecipients.Add(new AlertRuleRecipient
            {
                Id = Guid.NewGuid(),
                AlertRuleId = rule.Id,
                RecipientType = "User",
                RecipientId = userId,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ChangeTracker.Clear();
        return await GetByIdAsync(id, cancellationToken);
    }

    private static string NormalizeLevel(string level)
    {
        return level switch
        {
            "Info" => "Info",
            "Warning" => "Warning",
            "Critical" => "Critical",
            _ => throw new ArgumentOutOfRangeException(nameof(level), "Alert rule level must be Info, Warning, or Critical.")
        };
    }

    private async Task<AlertRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var rules = await dbContext.AlertRules
            .Include(rule => rule.Recipients)
            .AsNoTracking()
            .Where(rule => rule.Id == id)
            .ToArrayAsync(cancellationToken);
        var items = await ToDtosAsync(rules, cancellationToken);
        return items.SingleOrDefault();
    }

    private async Task<AlertRuleDto[]> ToDtosAsync(
        IReadOnlyList<AlertRule> rules,
        CancellationToken cancellationToken)
    {
        var roleIds = rules
            .SelectMany(rule => rule.Recipients)
            .Where(recipient => recipient.RecipientType == "Role")
            .Select(recipient => recipient.RecipientId)
            .Distinct()
            .ToList();
        var roleNames = await dbContext.Roles
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .ToDictionaryAsync(role => role.Id, role => role.Code, cancellationToken);

        var userIds = rules
            .SelectMany(rule => rule.Recipients)
            .Where(recipient => recipient.RecipientType == "User")
            .Select(recipient => recipient.RecipientId)
            .Distinct()
            .ToList();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.UserName, cancellationToken);

        return rules
            .Select(rule => ToDto(rule, roleNames, userNames))
            .ToArray();
    }

    private static AlertRuleDto ToDto(
        AlertRule rule,
        IReadOnlyDictionary<Guid, string> roleNames,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        var recipients = rule.Recipients
            .OrderBy(recipient => recipient.RecipientType == "Role" ? 0 : 1)
            .ThenBy(recipient => recipient.RecipientId)
            .Select(recipient => new AlertRuleRecipientDto(
                recipient.Id.ToString(),
                recipient.RecipientType,
                recipient.RecipientId.ToString(),
                GetRecipientName(recipient, roleNames, userNames)))
            .ToArray();

        return new AlertRuleDto(
            rule.Id.ToString(),
            rule.Code,
            rule.Name,
            rule.Description,
            rule.Metric,
            rule.Operator,
            rule.Threshold,
            rule.WindowMinutes,
            rule.Level,
            rule.Enabled,
            rule.NotifyEnabled,
            rule.EmailEnabled,
            rule.Sort,
            rule.Remark,
            recipients,
            rule.CreatedAt,
            rule.UpdatedAt);
    }

    private static string GetRecipientName(
        AlertRuleRecipient recipient,
        IReadOnlyDictionary<Guid, string> roleNames,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        return recipient.RecipientType switch
        {
            "Role" when roleNames.TryGetValue(recipient.RecipientId, out var roleName) => roleName,
            "User" when userNames.TryGetValue(recipient.RecipientId, out var userName) => userName,
            _ => recipient.RecipientId.ToString()
        };
    }
}
