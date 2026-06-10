using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Auth;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfAuthRepository(MiniAdminDbContext dbContext) : IAuthRepository
{
    public async Task<AuthenticatedUserDto?> FindByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RoleMenus)
            .ThenInclude(x => x.Menu)
            .SingleOrDefaultAsync(x => x.UserName == userName && x.IsEnabled, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleCodes = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsEnabled)
            .Select(x => x.Code)
            .Distinct()
            .Order()
            .ToArray();

        var permissionCodes = user.UserRoles
            .Where(x => x.Role.IsEnabled)
            .SelectMany(x => x.Role.RoleMenus)
            .Select(x => x.Menu)
            .Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.PermissionCode))
            .Select(x => x.PermissionCode!)
            .Distinct()
            .Order()
            .ToArray();

        return new AuthenticatedUserDto(
            user.Id.ToString(),
            user.TenantId,
            user.UserName,
            user.RealName,
            user.PasswordHash,
            user.SecurityStamp,
            roleCodes,
            permissionCodes);
    }
}
