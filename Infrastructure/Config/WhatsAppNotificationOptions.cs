namespace CarHub.Api.Infrastructure.Config;

public sealed class WhatsAppNotificationOptions
{
    public const string SectionName = "Notifications:WhatsApp";

    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string SenderNumber { get; set; } = string.Empty;
}