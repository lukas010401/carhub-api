using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Application.Contracts.Admin;

public sealed class SetUserRoleRequest
{
    public UserRole Role { get; set; }
}
