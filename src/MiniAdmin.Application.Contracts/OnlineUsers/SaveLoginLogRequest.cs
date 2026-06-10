namespace MiniAdmin.Application.Contracts.OnlineUsers;

public sealed record SaveLoginLogRequest(
    string UserName,
    bool IsSuccess,
    string Message,
    string? IpAddress,
    string? UserAgent,
    Guid? SessionId = null);
