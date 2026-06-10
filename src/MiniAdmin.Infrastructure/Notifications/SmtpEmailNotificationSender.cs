using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed class SmtpEmailNotificationSender(IOptions<EmailNotificationOptions> options)
    : IEmailNotificationSender
{
    public async Task SendAsync(
        string recipientEmail,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        var emailOptions = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(emailOptions.FromEmail.Trim(), emailOptions.FromName.Trim()),
            Subject = title,
            Body = content,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail.Trim()));

        using var client = new SmtpClient(emailOptions.Host.Trim(), emailOptions.Port)
        {
            EnableSsl = emailOptions.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(emailOptions.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(emailOptions.UserName.Trim(), emailOptions.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
