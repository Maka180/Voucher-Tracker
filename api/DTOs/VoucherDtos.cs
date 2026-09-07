namespace VoucherTracker.Api.DTOs;

public record CreateVoucherRequest(decimal Amount, string RecipientPhone);
public record FraudFlagResponse(int Id, string FlagType, string? AiExplanation, DateTime FlaggedAt, bool Resolved);

public record VoucherResponse(
    int Id,
    decimal Amount,
    string RecipientPhone,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? Pin // only returned once, at creation time
);


public record FlaggedVoucherResponse(
    int Id, decimal Amount, string RecipientPhone, string Status,
    DateTime CreatedAt, List<FraudFlagResponse> Flags
);