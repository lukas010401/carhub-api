using CarHub.Api.Application.Contracts.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CarHub.Api.Controllers;

public abstract class AdminControllerBase : ControllerBase
{
    protected (Guid UserId, string Email) GetAdminIdentity()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("Invalid authenticated user identifier.");
        }

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? "admin@unknown.local";

        return (userId, email);
    }

    protected IActionResult OkResponse<T>(T data, string message) =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult BadRequestResponse(string message, params string[] errors) =>
        BadRequest(ApiResponse<object>.Fail(message, errors));

    protected IActionResult NotFoundResponse(string message, params string[] errors) =>
        NotFound(ApiResponse<object>.Fail(message, errors));
}
