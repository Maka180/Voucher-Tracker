using System.Security.Cryptography;

namespace VoucherTracker.Api.Services;

public class PinService
{
    public string GeneratePin()
    {
        // Cryptographically secure random 6-digit PIN, e.g. "042817"
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6");
    }
}