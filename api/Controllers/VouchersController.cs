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

    public VouchersController(AppDbContext db, PinService pinService)
    {
        _db = db;
        _pinService = pinService;
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
}