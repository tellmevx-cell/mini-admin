using MiniAdmin.Application.Contracts.Menus;

namespace MiniAdmin.Application.Contracts.Caching;

public interface IUserAuthorizationCache
{
    Task<string?> GetSecurityStampAsync(
        Guid userId,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        string userName,
        Func<CancellationToken, Task<IReadOnlyList<string>>> factory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VbenMenuDto>> GetMenusAsync(
        string userName,
        Func<CancellationToken, Task<IReadOnlyList<VbenMenuDto>>> factory,
        CancellationToken cancellationToken = default);

    Task RemoveUserAsync(
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default);
}
