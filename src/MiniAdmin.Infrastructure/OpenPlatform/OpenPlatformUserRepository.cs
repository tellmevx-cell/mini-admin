using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public sealed class OpenPlatformUserRepository(
    MiniAdminDbContext dbContext) : IOpenPlatformUserRepository
{
    public async Task<OpenPlatformUserDto?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                    .ThenInclude(item => item.RoleMenus)
                        .ThenInclude(item => item.Menu)
            .SingleOrDefaultAsync(item => item.Id == id && item.IsEnabled, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles
            .Where(item => item.Role.IsEnabled)
            .Select(item => item.Role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = user.UserRoles
            .Where(item => item.Role.IsEnabled)
            .SelectMany(item => item.Role.RoleMenus)
            .Where(item => item.Menu.IsEnabled && !string.IsNullOrWhiteSpace(item.Menu.PermissionCode))
            .Select(item => item.Menu.PermissionCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new OpenPlatformUserDto(
            user.Id,
            user.TenantId,
            user.UserName,
            user.RealName,
            user.Email,
            user.SecurityStamp,
            roles,
            permissions);
    }
}
