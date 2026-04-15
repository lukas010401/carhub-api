namespace CarHub.Api.Infrastructure.Config;

public sealed class EmailNotificationOptions
{
    public const string SectionName = "Notifications:Email";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "https://localhost:3000";
}
