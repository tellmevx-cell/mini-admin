using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Caching;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class TenantSessionInvalidator(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache)
{
    public async Task InvalidateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users
            .Where(item => item.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        foreach (var user in users)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await userAuthorizationCache.RemoveUserAsync(
                user.Id,
                user.UserName,
                cancellationToken);
        }

        var onlineUsers = await (
                from onlineUser in dbContext.OnlineUsers
                join user in dbContext.Users on onlineUser.UserId equals user.Id
                where user.TenantId == tenantId && onlineUser.IsOnline
                select onlineUser)
            .ToArrayAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var onlineUser in onlineUsers)
        {
            onlineUser.IsOnline = false;
            onlineUser.LastActiveAt = now;
        }
    }
}
