namespace VoucherTracker.Api.DTOs;

public record CreateVoucherRequest(decimal Amount, string RecipientPhone);

public record VoucherResponse(
    int Id,
    decimal Amount,
    string RecipientPhone,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? Pin // only returned once, at creation time
);