using CarHub.Api.Domain.Entities;
using CarHub.Api.Infrastructure.Persistence;
using System.Text.Json;

namespace CarHub.Api.Infrastructure.Audit;

public sealed class AdminAuditService(AppDbContext dbContext) : IAdminAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task LogAsync(
        Guid adminUserId,
        string adminEmail,
        string action,
        string entityType,
        Guid? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            AdminEmail = adminEmail,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, SerializerOptions)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
