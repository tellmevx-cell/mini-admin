using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfUserNotificationRepository(MiniAdminDbContext dbContext) : IUserNotificationRepository
{
    public async Task<UserNotificationListResult> GetListAsync(
        Guid userId,
        UserNotificationListQuery query,
        CancellationToken cancellationToken = default)
    {
        var notificationsQuery = dbContext.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        var unreadCount = await notificationsQuery.CountAsync(
            notification => !notification.IsRead,
            cancellationToken);

        if (query.IsRead.HasValue)
        {
            notificationsQuery = notificationsQuery.Where(notification => notification.IsRead == query.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            notificationsQuery = notificationsQuery.Where(notification => notification.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = query.SourceType.Trim();
            notificationsQuery = notificationsQuery.Where(notification => notification.SourceType == sourceType);
        }

        var total = await notificationsQuery.CountAsync(cancellationToken);
        var page = query.Take.HasValue ? 1 : Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.Take ?? query.PageSize, 1, 100);
        var items = await notificationsQuery
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => ToDto(notification))
            .ToArrayAsync(cancellationToken);

        return new UserNotificationListResult(items, total, unreadCount);
    }

    public async Task<int> CreateForRoleAsync(
        string roleCode,
        IReadOnlyList<CreateUserNotificationRequest> requests,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return 0;
        }

        var userIds = await dbContext.Users
            .Where(user =>
                user.IsEnabled &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Code == roleCode &&
                    userRole.Role.IsEnabled))
            .Select(user => user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return await CreateForUsersAsync(userIds, requests, now, cancellationToken);
    }

    public async Task<int> CreateForUsersAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<CreateUserNotificationRequest> requests,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0 || requests.Count == 0)
        {
            return 0;
        }

        var normalizedUserIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        var created = 0;
        foreach (var userId in normalizedUserIds)
        {
            foreach (var request in requests)
            {
                var exists = await dbContext.UserNotifications.AnyAsync(
                    notification =>
                        notification.UserId == userId &&
                        notification.SourceType == request.SourceType &&
                        notification.SourceId == request.SourceId,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                dbContext.UserNotifications.Add(new UserNotification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = request.Title.Trim(),
                    Message = request.Message.Trim(),
                    Category = request.Category.Trim(),
                    Level = request.Level.Trim(),
                    Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
                    SourceType = request.SourceType.Trim(),
                    SourceId = request.SourceId.Trim(),
                    IsRead = false,
                    CreatedAt = now
                });
                created++;
            }
        }

        if (created > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(
            item => item.UserId == userId && item.Id == id,
            cancellationToken);
        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await dbContext.UserNotifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToArrayAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return unreadNotifications.Length;
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(
            item => item.UserId == userId && item.Id == id,
            cancellationToken);
        if (notification is null)
        {
            return false;
        }

        dbContext.UserNotifications.Remove(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await dbContext.UserNotifications
            .Where(notification => notification.UserId == userId)
            .ToArrayAsync(cancellationToken);
        dbContext.UserNotifications.RemoveRange(notifications);
        await dbContext.SaveChangesAsync(cancellationToken);
        return notifications.Length;
    }

    private static UserNotificationDto ToDto(UserNotification notification)
    {
        return new UserNotificationDto(
            notification.Id.ToString(),
            notification.Title,
            notification.Message,
            notification.Category,
            notification.Level,
            notification.Link,
            notification.SourceType,
            notification.SourceId,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
