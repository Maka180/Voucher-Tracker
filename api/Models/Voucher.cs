namespace VoucherTracker.Api.Models;

public class Voucher
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public User? Sender { get; set; }

    public decimal Amount { get; set; }
    public string RecipientPhone { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // Pending, Redeemed, Expired, Flagged

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }

    public ICollection<RedemptionAttempt> RedemptionAttempts { get; set; } = new List<RedemptionAttempt>();
    public ICollection<FraudFlag> FraudFlags { get; set; } = new List<FraudFlag>();
}