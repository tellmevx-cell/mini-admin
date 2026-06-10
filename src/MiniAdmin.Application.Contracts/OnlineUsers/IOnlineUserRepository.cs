using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.OnlineUsers;

public interface IOnlineUserRepository
{
    Task<PageResult<LoginLogDto>> GetLoginLogsAsync(
        LoginLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<PageResult<OnlineUserDto>> GetOnlineUsersAsync(
        OnlineUserListQuery query,
        CancellationToken cancellationToken = default);

    Task RecordLoginAsync(
        SaveLoginLogRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> TouchAsync(
        Guid sessionId,
        Guid userId,
        string userName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> ForceLogoutAsync(
        Guid userId,
        string? currentUserName = null,
        CancellationToken cancellationToken = default);

    Task<bool> ForceLogoutSessionAsync(
        Guid sessionId,
        string? currentUserName = null,
        CancellationToken cancellationToken = default);
}
