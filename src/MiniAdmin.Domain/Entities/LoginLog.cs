namespace MiniAdmin.Domain.Entities;

public sealed class LoginLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? RealName { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
