using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.OnlineUsers;

namespace MiniAdmin.Application.OnlineUsers;

public sealed class OnlineUserAppService(IOnlineUserRepository onlineUserRepository) : IOnlineUserAppService
{
    public Task<PageResult<LoginLogDto>> GetLoginLogsAsync(
        LoginLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.GetLoginLogsAsync(query, cancellationToken);
    }

    public Task<PageResult<OnlineUserDto>> GetOnlineUsersAsync(
        OnlineUserListQuery query,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.GetOnlineUsersAsync(query, cancellationToken);
    }

    public Task RecordLoginAsync(
        SaveLoginLogRequest request,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.RecordLoginAsync(request, cancellationToken);
    }

    public Task<bool> TouchAsync(
        Guid sessionId,
        Guid userId,
        string userName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.TouchAsync(sessionId, userId, userName, ipAddress, userAgent, cancellationToken);
    }

    public Task SignOutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.SignOutAsync(sessionId, cancellationToken);
    }

    public Task<bool> ForceLogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.ForceLogoutAsync(userId, null, cancellationToken);
    }

    public Task<bool> ForceLogoutSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.ForceLogoutSessionAsync(sessionId, null, cancellationToken);
    }

    public Task<bool> ForceLogoutAsync(
        Guid userId,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.ForceLogoutAsync(userId, currentUserName, cancellationToken);
    }

    public Task<bool> ForceLogoutSessionAsync(
        Guid sessionId,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        return onlineUserRepository.ForceLogoutSessionAsync(sessionId, currentUserName, cancellationToken);
    }
}
