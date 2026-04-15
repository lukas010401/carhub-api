using CarHub.Api.Application.Contracts.Admin;
using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Audit;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/users")]
public sealed class AdminUsersController(
    AppDbContext dbContext,
    IAdminAuditService auditService) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserQuery query)
    {
        var safePage = Math.Max(1, query.Page);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = dbContext.Users.AsNoTracking().AsQueryable();

        if (query.Role is not null)
        {
            baseQuery = baseQuery.Where(x => x.Role == query.Role.Value);
        }

        if (query.IsActive is not null)
        {
            baseQuery = baseQuery.Where(x => x.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                x.Email.ToLower().Contains(keyword) ||
                x.FullName.ToLower().Contains(keyword));
        }

        baseQuery = baseQuery.OrderByDescending(x => x.CreatedAt);

        var total = await baseQuery.CountAsync();
        var items = await baseQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                x.PhoneNumber,
                x.WhatsAppNumber,
                Role = x.Role.ToString(),
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Users loaded.");
    }

    [HttpPatch("{id:guid}/activation")]
    public async Task<IActionResult> SetActivation(Guid id, [FromBody] SetUserActivationRequest request)
    {
        var admin = GetAdminIdentity();
        if (admin.UserId == id && !request.IsActive)
        {
            return BadRequestResponse("Operation not allowed.", "You cannot deactivate your own account.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFoundResponse("User not found.");
        }

        user.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            admin.UserId,
            admin.Email,
            action: "user.activation.changed",
            entityType: "user",
            entityId: user.Id,
            details: new { user.IsActive });

        return OkResponse(new { user.Id, user.IsActive }, "User activation updated.");
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] SetUserRoleRequest request)
    {
        var admin = GetAdminIdentity();

        if (admin.UserId == id)
        {
            return BadRequestResponse("Operation not allowed.", "You cannot change your own role.");
        }

        if (request.Role is not (UserRole.Admin or UserRole.Seller))
        {
            return BadRequestResponse("Invalid role.", "Only Admin and Seller roles are allowed.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFoundResponse("User not found.");
        }

        if (user.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            var activeAdminsCount = await dbContext.Users.CountAsync(x => x.Role == UserRole.Admin && x.IsActive);
            if (activeAdminsCount <= 1 && user.IsActive)
            {
                return BadRequestResponse("Operation not allowed.", "Cannot remove the last active admin.");
            }
        }

        var previousRole = user.Role;
        user.Role = request.Role;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            admin.UserId,
            admin.Email,
            action: "user.role.changed",
            entityType: "user",
            entityId: user.Id,
            details: new { previousRole = previousRole.ToString(), newRole = user.Role.ToString() });

        return OkResponse(new { user.Id, Role = user.Role.ToString() }, "User role updated.");
    }
}

