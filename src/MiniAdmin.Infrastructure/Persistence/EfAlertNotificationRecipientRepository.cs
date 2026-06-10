using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfAlertNotificationRecipientRepository(MiniAdminDbContext dbContext)
    : IAlertNotificationRecipientRepository
{
    public async Task<IReadOnlyList<Guid>> ResolveUserIdsAsync(
        AlertRuleDto rule,
        CancellationToken cancellationToken = default)
    {
        var roleIds = ParseRecipientIds(rule, "Role");
        var directUserIds = ParseRecipientIds(rule, "User");
        var resolvedUserIds = new HashSet<Guid>();

        if (roleIds.Count > 0)
        {
            var roleUserIds = await dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.IsEnabled &&
                    user.UserRoles.Any(userRole =>
                        roleIds.Contains(userRole.RoleId) &&
                        userRole.Role.IsEnabled))
                .Select(user => user.Id)
                .ToArrayAsync(cancellationToken);
            foreach (var userId in roleUserIds)
            {
                resolvedUserIds.Add(userId);
            }
        }

        if (directUserIds.Count > 0)
        {
            var enabledDirectUserIds = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.IsEnabled && directUserIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToArrayAsync(cancellationToken);
            foreach (var userId in enabledDirectUserIds)
            {
                resolvedUserIds.Add(userId);
            }
        }

        return resolvedUserIds.Count > 0
            ? resolvedUserIds.ToArray()
            : await ResolveAdminUserIdsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ResolveAdminUserIdsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.IsEnabled &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Code == "admin" &&
                    userRole.Role.IsEnabled))
            .Select(user => user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private static List<Guid> ParseRecipientIds(AlertRuleDto rule, string recipientType)
    {
        return rule.Recipients
            .Where(recipient =>
                recipient.RecipientType.Equals(recipientType, StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(recipient.RecipientId, out _))
            .Select(recipient => Guid.Parse(recipient.RecipientId))
            .Distinct()
            .ToList();
    }
}
