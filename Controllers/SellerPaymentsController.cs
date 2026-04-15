using System.Security.Claims;
using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Application.Contracts.Payments;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Config;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "SellerOrAdminPolicy")]
[Route("api/seller/payments")]
public sealed class SellerPaymentsController(AppDbContext dbContext, IOptions<ManualPaymentOptions> options) : ControllerBase
{
    private readonly ManualPaymentOptions _options = options.Value;

    [HttpPost("initiate-listing")]
    public async Task<IActionResult> InitiateListingPayment([FromBody] InitiateListingPaymentRequest request)
    {
        var userId = GetUserId();
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFound(ApiResponse<object>.Fail("Seller not found."));

        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == request.ListingId && x.SellerId == userId);
        if (listing is null) return NotFound(ApiResponse<object>.Fail("Listing not found."));

        if (user.AccountType == AccountType.Professional)
        {
            return BadRequest(ApiResponse<object>.Fail("Professional accounts must use subscription renewal payment flow."));
        }

        if (listing.Status == ListingStatus.Published || listing.Status == ListingStatus.PendingReview)
        {
            return BadRequest(ApiResponse<object>.Fail("Listing is already published or in publication workflow."));
        }

        var activePending = await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.ListingId == listing.Id
                && x.Type == ManualPaymentType.ListingPublication
                && (x.Status == ManualPaymentStatus.Initiated || x.Status == ManualPaymentStatus.ProofSubmitted || x.Status == ManualPaymentStatus.UnderReview)
                && x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (activePending is not null)
        {
            return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(activePending), "Existing pending payment found."));
        }

        var payment = new ManualPaymentRequest
        {
            UserId = userId,
            ListingId = listing.Id,
            Type = ManualPaymentType.ListingPublication,
            Status = ManualPaymentStatus.Initiated,
            ExpectedAmount = _options.IndividualListingFee,
            InternalReference = GenerateInternalReference("LST"),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, _options.RequestExpiryMinutes))
        };

        dbContext.ManualPaymentRequests.Add(payment);
        await dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(payment), "Payment request created."));
    }

    [HttpPost("initiate-subscription")]
    public async Task<IActionResult> InitiateSubscriptionPayment([FromBody] InitiateSubscriptionPaymentRequest request)
    {
        var userId = GetUserId();
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFound(ApiResponse<object>.Fail("Seller not found."));

        if (user.AccountType != AccountType.Professional)
        {
            return BadRequest(ApiResponse<object>.Fail("Only professional sellers can initiate subscription renewal payment."));
        }

        var months = Math.Clamp(request.Months <= 0 ? 1 : request.Months, 1, 24);
        var monthlyPrice = _options.ProfessionalMonthlyFee < 0 ? 0 : _options.ProfessionalMonthlyFee;
        var expectedAmount = monthlyPrice * months;

        var activePending = await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.Type == ManualPaymentType.ProfessionalSubscriptionRenewal
                && (x.Status == ManualPaymentStatus.Initiated || x.Status == ManualPaymentStatus.ProofSubmitted || x.Status == ManualPaymentStatus.UnderReview)
                && x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (activePending is not null)
        {
            return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(activePending), "Existing pending payment found."));
        }

        var payment = new ManualPaymentRequest
        {
            UserId = userId,
            Type = ManualPaymentType.ProfessionalSubscriptionRenewal,
            Status = ManualPaymentStatus.Initiated,
            ExpectedAmount = expectedAmount,
            RequestedMonths = months,
            RequestedMonthlyPrice = monthlyPrice,
            InternalReference = GenerateInternalReference("SUB"),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, _options.RequestExpiryMinutes))
        };

        dbContext.ManualPaymentRequests.Add(payment);
        await dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(payment), "Payment request created."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMyPayment(Guid id)
    {
        var userId = GetUserId();
        var payment = await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (payment is null) return NotFound(ApiResponse<object>.Fail("Payment request not found."));
        return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(payment), "Payment request loaded."));
    }

    [HttpPost("{id:guid}/submit-proof")]
    public async Task<IActionResult> SubmitProof(Guid id, [FromForm] SubmitManualPaymentProofRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var payment = await dbContext.ManualPaymentRequests
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (payment is null) return NotFound(ApiResponse<object>.Fail("Payment request not found."));

        if (payment.Status is ManualPaymentStatus.Approved or ManualPaymentStatus.Cancelled or ManualPaymentStatus.Expired)
        {
            return BadRequest(ApiResponse<object>.Fail("Payment request status does not allow proof submission."));
        }

        if (payment.ExpiresAtUtc < DateTime.UtcNow)
        {
            payment.Status = ManualPaymentStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return BadRequest(ApiResponse<object>.Fail("Payment request expired."));
        }

        var txRef = request.ProviderTransactionReference?.Trim() ?? string.Empty;
        var senderNumber = request.SenderNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(txRef) || string.IsNullOrWhiteSpace(senderNumber))
        {
            return BadRequest(ApiResponse<object>.Fail("ProviderTransactionReference and SenderNumber are required."));
        }

        var duplicateTx = await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .AnyAsync(x => x.Id != payment.Id && x.ProviderTransactionReference == txRef, cancellationToken);
        if (duplicateTx)
        {
            return BadRequest(ApiResponse<object>.Fail("Transaction reference already used."));
        }

        payment.Provider = request.Provider;
        payment.ReceiverNumber = GetReceiverForProvider(request.Provider);
        payment.ProviderTransactionReference = txRef;
        payment.SenderNumber = senderNumber;
        payment.SenderName = request.SenderName?.Trim();
        var paidAtUtc = request.PaidAtLocal.Kind switch
        {
            DateTimeKind.Utc => request.PaidAtLocal,
            DateTimeKind.Local => request.PaidAtLocal.ToUniversalTime(),
            _ => DateTime.SpecifyKind(request.PaidAtLocal, DateTimeKind.Local).ToUniversalTime()
        };
        payment.PaidAtLocal = paidAtUtc;
        payment.Notes = request.Notes?.Trim();
        payment.ProofFileUrl = null;
        payment.ProofFileHash = null;
        payment.SubmittedAtUtc = DateTime.UtcNow;
        payment.Status = ManualPaymentStatus.UnderReview;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(ToSellerPaymentDto(payment), "Payment proof submitted. Awaiting admin review."));
    }

    private object ToSellerPaymentDto(ManualPaymentRequest payment)
    {
        return new
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
            payment.ProviderTransactionReference,
            payment.SenderNumber,
            payment.SenderName,
            payment.PaidAtLocal,
            payment.Notes,
            payment.ProofFileUrl,
            payment.ListingId,
            receiverNumbers = new
            {
                yas = _options.YasReceiverNumber,
                orange = _options.OrangeReceiverNumber,
                airtel = _options.AirtelReceiverNumber
            }
        };
    }

    private string GetReceiverForProvider(MobileMoneyProvider provider) => provider switch
    {
        MobileMoneyProvider.Yas => _options.YasReceiverNumber,
        MobileMoneyProvider.Orange => _options.OrangeReceiverNumber,
        MobileMoneyProvider.Airtel => _options.AirtelReceiverNumber,
        _ => string.Empty
    };

    private static string GenerateInternalReference(string prefix)
        => $"CH-{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}


