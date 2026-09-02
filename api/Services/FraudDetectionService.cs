using Microsoft.EntityFrameworkCore;
using VoucherTracker.Api.Data;
using VoucherTracker.Api.Models;

namespace VoucherTracker.Api.Services;

public class FraudDetectionService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FraudDetectionService> _logger;

    public FraudDetectionService(IServiceProvider services, ILogger<FraudDetectionService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForFraudAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fraud scan failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task ScanForFraudAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddHours(-1);

        // Bait pattern: 3+ small vouchers (<= R150) to the same recipient within the last hour
        var baitGroups = await db.Vouchers
            .Where(v => v.Amount <= 150 && v.CreatedAt > cutoff && v.Status != "Flagged")
            .GroupBy(v => v.RecipientPhone)
            .Where(g => g.Count() >= 3)
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var recipientPhone in baitGroups)
        {
            var vouchers = await db.Vouchers
                .Where(v => v.RecipientPhone == recipientPhone && v.Amount <= 150 && v.CreatedAt > cutoff && v.Status != "Flagged")
                .ToListAsync(ct);

            foreach (var voucher in vouchers)
            {
                voucher.Status = "Flagged";
                db.FraudFlags.Add(new FraudFlag
                {
                    VoucherId = voucher.Id,
                    FlagType = "BaitPattern"
                });
            }

            _logger.LogWarning("Bait pattern flagged for recipient {Phone}: {Count} small vouchers", recipientPhone, vouchers.Count);
        }

        // Duplicate PIN sharing: redemption attempts on the same voucher from 2+ distinct IPs within 5 min
        var recentAttemptCutoff = DateTime.UtcNow.AddMinutes(-5);
        var voucherIds = await db.RedemptionAttempts
            .Where(a => a.AttemptedAt > recentAttemptCutoff)
            .GroupBy(a => a.VoucherId)
            .Where(g => g.Select(a => a.IpAddress).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var voucherId in voucherIds)
        {
            var alreadyFlagged = await db.FraudFlags
                .AnyAsync(f => f.VoucherId == voucherId && f.FlagType == "DuplicatePin" && !f.Resolved, ct);

            if (alreadyFlagged) continue;

            var voucher = await db.Vouchers.FindAsync(new object[] { voucherId }, ct);
            if (voucher == null || voucher.Status == "Flagged") continue;

            voucher.Status = "Flagged";
            db.FraudFlags.Add(new FraudFlag
            {
                VoucherId = voucherId,
                FlagType = "DuplicatePin"
            });

            _logger.LogWarning("Duplicate PIN sharing flagged for voucher {Id}", voucherId);
        }

        await db.SaveChangesAsync(ct);
    }
}