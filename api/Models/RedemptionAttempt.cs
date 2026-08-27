namespace VoucherTracker.Api.Models;

public class RedemptionAttempt
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public bool Success { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}