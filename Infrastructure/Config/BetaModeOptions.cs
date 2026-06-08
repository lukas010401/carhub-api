namespace CarHub.Api.Infrastructure.Config;
public sealed class BetaModeOptions
{
    public const string SectionName = "BetaMode";
    public bool Enabled { get; set; } = false;
}