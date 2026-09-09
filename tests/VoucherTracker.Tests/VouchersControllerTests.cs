using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoucherTracker.Api.Controllers;
using VoucherTracker.Api.Data;
using VoucherTracker.Api.DTOs;
using VoucherTracker.Api.Models;
using VoucherTracker.Api.Services;
using Xunit;

namespace VoucherTracker.Tests;

public class VouchersControllerTests
{
    private static AppDbContext NewInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // unique DB per test
            .Options;
        return new AppDbContext(options);
    }

    private static VouchersController NewController(AppDbContext db, int userId = 1)
    {
        var controller = new VouchersController(db, new PinService(), new AuditService(db));

        var claims = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
        });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) }
        };

        return controller;
    }

    [Fact]
    public async Task CreateVoucher_RejectsZeroOrNegativeAmount()
    {
        using var db = NewInMemoryDb();
        var controller = NewController(db);

        var result = await controller.CreateVoucher(new CreateVoucherRequest(0, "0821234567"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateVoucher_HashesThePin_NeverStoresPlaintext()
    {
        using var db = NewInMemoryDb();
        db.Users.Add(new User { Id = 1, FullName = "Test", Phone = "0821234567", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.CreateVoucher(new CreateVoucherRequest(100, "0839876543"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<VoucherResponse>(ok.Value);

        var savedVoucher = await db.Vouchers.FindAsync(response.Id);
        Assert.NotNull(savedVoucher);
        Assert.NotEqual(response.Pin, savedVoucher!.PinHash); // hash must differ from plaintext
        Assert.True(BCrypt.Net.BCrypt.Verify(response.Pin, savedVoucher.PinHash)); // but should verify correctly
    }

    [Fact]
    public async Task RedeemVoucher_WrongPin_ReturnsRemainingAttempts()
    {
        using var db = NewInMemoryDb();
        var voucher = new Voucher
        {
            SenderId = 1,
            Amount = 100,
            RecipientPhone = "0839876543",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("000000"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RedemptionResponse>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Contains("2 attempt", response.Message);
    }

    [Fact]
    public async Task RedeemVoucher_CorrectPin_MarksRedeemedAndReturnsAmount()
    {
        using var db = NewInMemoryDb();
        var voucher = new Voucher
        {
            SenderId = 1,
            Amount = 250,
            RecipientPhone = "0839876543",
            PinHash = BCrypt.Net.BCrypt.HashPassword("654321"),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("654321"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RedemptionResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(250, response.Amount);

        var updated = await db.Vouchers.FindAsync(voucher.Id);
        Assert.Equal("Redeemed", updated!.Status);
    }

    [Fact]
    public async Task RedeemVoucher_FourthFailedAttempt_LocksVoucher()
    {
        using var db = NewInMemoryDb();
        var voucher = new Voucher
        {
            SenderId = 1,
            Amount = 100,
            RecipientPhone = "0839876543",
            PinHash = BCrypt.Net.BCrypt.HashPassword("999999"),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var controller = NewController(db);

        // First 3 wrong attempts are allowed through (counting down remaining attempts)
        await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("000000"));
        await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("111111"));
        await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("222222"));

        // The 4th attempt, after 3 prior failures, should be locked out
        var fourthAttempt = await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("333333"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(fourthAttempt.Result);
        var response = Assert.IsType<RedemptionResponse>(badRequest.Value);
        Assert.Contains("Too many failed attempts", response.Message);

        var updated = await db.Vouchers.FindAsync(voucher.Id);
        Assert.Equal("Flagged", updated!.Status);
    }

    [Fact]
    public async Task RedeemVoucher_ExpiredVoucher_ReturnsExpiredMessage()
    {
        using var db = NewInMemoryDb();
        var voucher = new Voucher
        {
            SenderId = 1,
            Amount = 100,
            RecipientPhone = "0839876543",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // already expired
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.RedeemVoucher(voucher.Id, new RedeemVoucherRequest("123456"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RedemptionResponse>(badRequest.Value);
        Assert.Contains("expired", response.Message);
    }
}