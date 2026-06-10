namespace MiniAdmin.Domain.Entities;

public sealed class OnlineUser
{
    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? DeviceName { get; set; }

    public string? BrowserName { get; set; }

    public DateTimeOffset LoginAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsOnline { get; set; } = true;
}
