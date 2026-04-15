using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Application.Contracts.Admin;

public sealed class AdminUserQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Keyword { get; set; }
}
