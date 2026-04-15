using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarHub.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace CarHub.Api.Infrastructure.Notifications;

public sealed class HttpSmsSender(HttpClient httpClient, IOptions<SmsNotificationOptions> options, ILogger<HttpSmsSender> logger) : ISmsSender
{
    private readonly SmsNotificationOptions _options = options.Value;

    public async Task SendAsync(string toNumber, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;
        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
        {
            logger.LogWarning("SMS notifications enabled but ApiUrl is empty.");
            return;
        }

        var payload = new
        {
            from = _options.SenderId,
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
            logger.LogWarning("SMS send failed. Status: {Status}, Body: {Body}", (int)response.StatusCode, body);
        }
    }
}
