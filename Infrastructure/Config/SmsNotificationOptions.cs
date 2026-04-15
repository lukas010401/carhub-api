namespace CarHub.Api.Infrastructure.Config;

public sealed class SmsNotificationOptions
{
    public const string SectionName = "Notifications:Sms";

    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
}
