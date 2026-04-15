namespace CarHub.Api.Infrastructure.Audit;

public interface IAdminAuditService
{
    Task LogAsync(
        Guid adminUserId,
        string adminEmail,
        string action,
        string entityType,
        Guid? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default);
}
