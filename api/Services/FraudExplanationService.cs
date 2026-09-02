using System.Text;
using System.Text.Json;
using VoucherTracker.Api.Models;

namespace VoucherTracker.Api.Services;

public interface IFraudExplanationService
{
    Task<string> ExplainAsync(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts);
}

public class GeminiFraudExplanationService : IFraudExplanationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiFraudExplanationService> _logger;

    public GeminiFraudExplanationService(HttpClient http, IConfiguration config, ILogger<GeminiFraudExplanationService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> ExplainAsync(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts)
    {
        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return $"[AI explanation unavailable — no API key configured] Flag type: {flag.FlagType}";
        }

        var prompt = BuildPrompt(flag, voucher, attempts);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        try
        {
            var response = await _http.PostAsync(url,
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API call failed: {Status} {Body}", response.StatusCode, body);
                return $"[AI explanation unavailable] Flag type: {flag.FlagType}";
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text?.Trim() ?? $"Flag type: {flag.FlagType}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API for fraud explanation");
            return $"[AI explanation unavailable] Flag type: {flag.FlagType}";
        }
    }

    private static string BuildPrompt(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts)
    {
        var attemptSummary = attempts.Count == 0
            ? "(no redemption attempts recorded yet)"
            : string.Join("\n", attempts.Select(a =>
                $"- {(a.Success ? "SUCCESS" : "FAILED")} at {a.AttemptedAt:HH:mm:ss} from IP {a.IpAddress}"));

        return $"""
            You are a fraud analyst assistant for a mobile money voucher platform.
            Explain in 2-3 plain-English sentences why this voucher was flagged, for a non-technical admin reviewing it.

            Flag type: {flag.FlagType}
            Voucher: R{voucher.Amount} to {voucher.RecipientPhone}
            Recent redemption attempts:
            {attemptSummary}

            Keep it factual and concise. Do not include any recommendations, only an explanation of the pattern observed.
            """;
    }
}