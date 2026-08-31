namespace VoucherTracker.Api.DTOs;

public record RedeemVoucherRequest(string Pin);

public record RedemptionResponse(bool Success, string Message, decimal? Amount = null);