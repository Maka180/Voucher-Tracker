namespace VoucherTracker.Api.Models;

public class FraudFlag
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public string FlagType { get; set; } = string.Empty; // DuplicatePin, BaitPattern, RapidRetry
    public DateTime FlaggedAt { get; set; } = DateTime.UtcNow;
    public bool Resolved { get; set; }
    public string? ResolvedBy { get; set; }

    // AI-generated plain-English explanation 
    public string? AiExplanation { get; set; }
}