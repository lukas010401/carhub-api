using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Application.Contracts.Payments;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Audit;
using CarHub.Api.Infrastructure.Notifications;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/payments")]
public sealed class AdminPaymentsController(
    AppDbContext dbContext,
    IAdminAuditService auditService,
    IPaymentNotificationService paymentNotificationService) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminManualPaymentQuery query)
    {
        var safePage = Math.Max(1, query.Page);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = dbContext.ManualPaymentRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Listing)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ManualPaymentStatus>(query.Status, true, out var parsedStatus))
        {
            q = q.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Type) && Enum.TryParse<ManualPaymentType>(query.Type, true, out var parsedType))
        {
            q = q.Where(x => x.Type == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.InternalReference.ToLower().Contains(kw)
                || (x.ProviderTransactionReference != null && x.ProviderTransactionReference.ToLower().Contains(kw))
                || (x.User != null && (x.User.FullName.ToLower().Contains(kw) || x.User.Email.ToLower().Contains(kw)))
            );
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                Type = x.Type.ToString(),
                Status = x.Status.ToString(),
                x.InternalReference,
                x.ExpectedAmount,
                x.ExpiresAtUtc,
                x.SubmittedAtUtc,
                x.Provider,
                x.ProviderTransactionReference,
                x.SenderNumber,
                x.SenderName,
                x.PaidAtLocal,
                x.ProofFileUrl,
                x.ListingId,
                x.RequestedMonths,
                x.RequestedMonthlyPrice,
                Seller = x.User == null ? null : new
                {
                    x.User.Id,
                    x.User.FullName,
                    x.User.Email,
                    x.User.PhoneNumber,
                    x.User.WhatsAppNumber,
                    AccountType = x.User.AccountType.ToString()
                },
                Listing = x.Listing == null ? null : new
                {
                    x.Listing.Id,
                    x.Listing.Title,
                    Status = x.Listing.Status.ToString()
                }
            })
            .ToListAsync();

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Manual payments loaded.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var payment = await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Listing)
            .Include(x => x.Decisions.OrderByDescending(d => d.CreatedAt))
            .SingleOrDefaultAsync(x => x.Id == id);

        if (payment is null) return NotFoundResponse("Payment request not found.");

        return OkResponse(new
        {
            payment.Id,
            Type = payment.Type.ToString(),
            Status = payment.Status.ToString(),
            payment.InternalReference,
            payment.ExpectedAmount,
            payment.RequestedMonths,
            payment.RequestedMonthlyPrice,
            payment.ExpiresAtUtc,
            payment.SubmittedAtUtc,
            payment.ReviewedAtUtc,
            payment.Provider,
            payment.ReceiverNumber,
            payment.ProviderTransactionReference,
            payment.SenderNumber,
            payment.SenderName,
            payment.PaidAtLocal,
            payment.ProofFileUrl,
            payment.ProofFileHash,
            payment.Notes,
            payment.ReviewNote,
            payment.ListingId,
            Seller = payment.User == null ? null : new
            {
                payment.User.Id,
                payment.User.FullName,
                payment.User.Email,
                payment.User.PhoneNumber,
                payment.User.WhatsAppNumber,
                AccountType = payment.User.AccountType.ToString()
            },
            Listing = payment.Listing == null ? null : new
            {
                payment.Listing.Id,
                payment.Listing.Title,
                Status = payment.Listing.Status.ToString()
            },
            Decisions = payment.Decisions.Select(d => new
            {
                d.Id,
                d.Action,
                d.AdminUserId,
                d.Note,
                d.CreatedAt
            })
        }, "Manual payment loaded.");
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] AdminReviewManualPaymentRequest request)
    {
        var payment = await dbContext.ManualPaymentRequests
            .Include(x => x.User)
            .Include(x => x.Listing)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (payment is null) return NotFoundResponse("Payment request not found.");
        if (payment.Status == ManualPaymentStatus.Approved) return OkResponse(new { payment.Id }, "Payment already approved.");
        if (payment.Status is ManualPaymentStatus.Expired or ManualPaymentStatus.Cancelled) return BadRequestResponse("Cannot approve this payment in its current status.");

        var admin = GetAdminIdentity();
        var now = DateTime.UtcNow;

        payment.Status = ManualPaymentStatus.Approved;
        payment.ReviewedAtUtc = now;
        payment.ReviewedByAdminId = admin.UserId;
        payment.ReviewNote = request.Note?.Trim();

        dbContext.ManualPaymentDecisions.Add(new ManualPaymentDecision
        {
            PaymentRequestId = payment.Id,
            AdminUserId = admin.UserId,
            Action = "Approved",
            Note = request.Note?.Trim()
        });

        if (payment.Type == ManualPaymentType.ListingPublication)
        {
            if (payment.Listing is null)
            {
                return BadRequestResponse("Listing not found for this payment.");
            }

            payment.Listing.Status = ListingStatus.Published;
            payment.Listing.RejectionReason = null;
            payment.Listing.PublishedAt = now;
        }
        else if (payment.Type == ManualPaymentType.ProfessionalSubscriptionRenewal)
        {
            if (payment.User is null)
            {
                return BadRequestResponse("Seller not found for this payment.");
            }

            var months = Math.Clamp(payment.RequestedMonths ?? 1, 1, 24);
            var monthlyPrice = payment.RequestedMonthlyPrice ?? 0m;

            var latest = await dbContext.ProfessionalSubscriptions
                .Where(x => x.UserId == payment.UserId)
                .OrderByDescending(x => x.EndsAtUtc)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            var startsAt = latest is not null && latest.EndsAtUtc > now ? latest.EndsAtUtc : now;
            dbContext.ProfessionalSubscriptions.Add(new ProfessionalSubscription
            {
                UserId = payment.UserId,
                PlanCode = latest?.PlanCode ?? "PRO_MONTHLY",
                MonthlyPrice = monthlyPrice,
                StartsAtUtc = startsAt,
                EndsAtUtc = startsAt.AddMonths(months),
                Status = SubscriptionStatus.Active,
                Notes = $"Approved from manual payment {payment.InternalReference}"
            });
        }

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "payment.manual.approved", "manual_payment", payment.Id,
            new { payment.Type, payment.InternalReference, payment.ExpectedAmount, payment.ListingId, payment.UserId });

        if (payment.User is not null)
        {
            await paymentNotificationService.NotifyPaymentReviewedAsync(payment, payment.User, approved: true);
        }

        return OkResponse(new { payment.Id, Status = payment.Status.ToString() }, "Payment approved.");
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] AdminReviewManualPaymentRequest request)
    {
        var payment = await dbContext.ManualPaymentRequests
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (payment is null) return NotFoundResponse("Payment request not found.");
        if (payment.Status == ManualPaymentStatus.Rejected) return OkResponse(new { payment.Id }, "Payment already rejected.");
        if (payment.Status == ManualPaymentStatus.Approved) return BadRequestResponse("Approved payment cannot be rejected.");

        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return BadRequestResponse("Reject note is required.");
        }

        var admin = GetAdminIdentity();
        payment.Status = ManualPaymentStatus.Rejected;
        payment.ReviewedAtUtc = DateTime.UtcNow;
        payment.ReviewedByAdminId = admin.UserId;
        payment.ReviewNote = note;

        dbContext.ManualPaymentDecisions.Add(new ManualPaymentDecision
        {
            PaymentRequestId = payment.Id,
            AdminUserId = admin.UserId,
            Action = "Rejected",
            Note = note
        });

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "payment.manual.rejected", "manual_payment", payment.Id,
            new { payment.Type, payment.InternalReference, payment.ExpectedAmount, payment.ListingId, payment.UserId, note });

        if (payment.User is not null)
        {
            await paymentNotificationService.NotifyPaymentReviewedAsync(payment, payment.User, approved: false);
        }

        return OkResponse(new { payment.Id, Status = payment.Status.ToString() }, "Payment rejected.");
    }
}

