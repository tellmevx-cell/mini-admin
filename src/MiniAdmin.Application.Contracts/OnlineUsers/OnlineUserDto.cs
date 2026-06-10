namespace MiniAdmin.Application.Contracts.OnlineUsers;

public sealed record OnlineUserDto(
    string SessionId,
    string UserId,
    string UserName,
    string RealName,
    string? IpAddress,
    string? UserAgent,
    string? DeviceName,
    string? BrowserName,
    DateTimeOffset LoginAt,
    DateTimeOffset LastActiveAt);
