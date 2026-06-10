namespace MiniAdmin.Application.Contracts.OnlineUsers;

public sealed record LoginLogDto(
    string Id,
    string? UserId,
    string UserName,
    string? RealName,
    string? IpAddress,
    string? UserAgent,
    bool IsSuccess,
    string Message,
    DateTimeOffset CreatedAt);
