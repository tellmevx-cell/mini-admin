namespace MiniAdmin.Infrastructure.Notifications;

public sealed class EmailNotificationOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 465;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "MiniAdmin";

    public bool EnableSsl { get; set; } = true;
}
