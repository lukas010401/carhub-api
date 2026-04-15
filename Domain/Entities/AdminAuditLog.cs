namespace CarHub.Api.Domain.Entities;

public sealed class AdminAuditLog : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
}
