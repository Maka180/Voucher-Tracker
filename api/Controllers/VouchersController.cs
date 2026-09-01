using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoucherTracker.Api.Data;
using VoucherTracker.Api.DTOs;
using VoucherTracker.Api.Models;
using VoucherTracker.Api.Services;


namespace VoucherTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VouchersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PinService _pinService;
    private readonly AuditService _audit;

    public VouchersController(AppDbContext db, PinService pinService, AuditService audit)
    {
        _db = db;
        _pinService = pinService;
        _audit = audit;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<ActionResult<VoucherResponse>> CreateVoucher(CreateVoucherRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be greater than zero.");

        var pin = _pinService.GeneratePin();

        var voucher = new Voucher
        {
            SenderId = CurrentUserId,
            Amount = request.Amount,
            RecipientPhone = request.RecipientPhone,
            PinHash = BCrypt.Net.BCrypt.HashPassword(pin),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(24) // vouchers expire after 24h
        };

        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId, "CreateVoucher", "Voucher", voucher.Id, $"Amount={voucher.Amount}, Recipient={voucher.RecipientPhone}");

        return Ok(new VoucherResponse(
            voucher.Id, voucher.Amount, voucher.RecipientPhone,
            voucher.Status, voucher.CreatedAt, voucher.ExpiresAt, pin));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<VoucherResponse>>> GetMyVouchers()
    {
        var vouchers = await _db.Vouchers
            .Where(v => v.SenderId == CurrentUserId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VoucherResponse(
                v.Id, v.Amount, v.RecipientPhone, v.Status, v.CreatedAt, v.ExpiresAt, null))
            .ToListAsync();

        return Ok(vouchers);
    }
    [HttpPost("{id}/redeem")]
[AllowAnonymous]
public async Task<ActionResult<RedemptionResponse>> RedeemVoucher(int id, RedeemVoucherRequest request)
{
    var voucher = await _db.Vouchers
        .Include(v => v.RedemptionAttempts)
        .FirstOrDefaultAsync(v => v.Id == id);

    if (voucher == null)
        return NotFound(new RedemptionResponse(false, "Voucher not found."));

    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = Request.Headers.UserAgent.ToString();

    // Already redeemed
    if (voucher.Status == "Redeemed")
        return BadRequest(new RedemptionResponse(false, "This voucher has already been redeemed."));

    // Expired
    if (voucher.Status != "Flagged" && DateTime.UtcNow > voucher.ExpiresAt)
    {
        voucher.Status = "Expired";
        await _db.SaveChangesAsync();
        return BadRequest(new RedemptionResponse(false, "This voucher has expired."));
    }

    // Locked due to fraud flag / too many failed attempts
    if (voucher.Status == "Flagged")
        return BadRequest(new RedemptionResponse(false, "This voucher is locked pending review."));

    // Rate limiting: count recent failed attempts (last 5 minutes)
    var recentFailures = voucher.RedemptionAttempts
        .Count(a => !a.Success && a.AttemptedAt > DateTime.UtcNow.AddMinutes(-5));

    if (recentFailures >= 3)
    {
        voucher.Status = "Flagged";
        _db.FraudFlags.Add(new FraudFlag
        {
            VoucherId = voucher.Id,
            FlagType = "RapidRetry"
        });
        await _db.SaveChangesAsync();
        return BadRequest(new RedemptionResponse(false, "Too many failed attempts. Voucher locked for review."));
    }

    var isCorrect = BCrypt.Net.BCrypt.Verify(request.Pin, voucher.PinHash);

    _db.RedemptionAttempts.Add(new RedemptionAttempt
    {
        VoucherId = voucher.Id,
        Success = isCorrect,
        DeviceInfo = userAgent,
        IpAddress = ip
    });

    if (!isCorrect)
    {
        await _db.SaveChangesAsync();
        var remaining = 3 - (recentFailures + 1);
        await _audit.LogAsync(null, "VoucherLocked", "Voucher", voucher.Id, $"IP={ip}");
        return BadRequest(new RedemptionResponse(false, $"Incorrect PIN. {remaining} attempt(s) remaining."));
    }

    voucher.Status = "Redeemed";
    await _audit.LogAsync(null, "RedeemVoucher", "Voucher", voucher.Id, $"Success, IP={ip}");
    voucher.RedeemedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    return Ok(new RedemptionResponse(true, "Voucher redeemed successfully.", voucher.Amount));
}
}