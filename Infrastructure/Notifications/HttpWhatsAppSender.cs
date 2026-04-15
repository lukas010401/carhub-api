using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarHub.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace CarHub.Api.Infrastructure.Notifications;

public sealed class HttpWhatsAppSender(HttpClient httpClient, IOptions<WhatsAppNotificationOptions> options, ILogger<HttpWhatsAppSender> logger) : IWhatsAppSender
{
    private readonly WhatsAppNotificationOptions _options = options.Value;

    public async Task SendAsync(string toNumber, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;
        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
        {
            logger.LogWarning("WhatsApp notifications enabled but ApiUrl is empty.");
            return;
        }

        var payload = new
        {
            from = _options.SenderNumber,
            to = toNumber,
            message
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("WhatsApp send failed. Status: {Status}, Body: {Body}", (int)response.StatusCode, body);
        }
    }
}