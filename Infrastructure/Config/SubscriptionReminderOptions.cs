namespace CarHub.Api.Infrastructure.Config;

public sealed class SubscriptionReminderOptions
{
    public const string SectionName = "Notifications:SubscriptionExpiryReminder";

    public bool Enabled { get; set; } = true;
    public int LeadTimeHours { get; set; } = 24;
    public int WindowMinutes { get; set; } = 90;
    public int CheckIntervalMinutes { get; set; } = 30;
}
