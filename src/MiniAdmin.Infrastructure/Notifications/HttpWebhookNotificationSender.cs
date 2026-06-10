using System.Net.Http;
using System.Text;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed class HttpWebhookNotificationSender : IWebhookNotificationSender
{
    private static readonly HttpClient HttpClient = new();

    public async Task SendAsync(
        string endpointUrl,
        string payloadJson,
        string? secret,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl.Trim())
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(secret))
        {
            request.Headers.TryAddWithoutValidation("X-MiniAdmin-Webhook-Secret", secret.Trim());
        }

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Webhook returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }
}
