using System.Text;
using System.Text.Json;
using VoucherTracker.Api.Models;

namespace VoucherTracker.Api.Services;

public interface IFraudExplanationService
{
    Task<string> ExplainAsync(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts);
}

public class ClaudeFraudExplanationService : IFraudExplanationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ClaudeFraudExplanationService> _logger;

    public ClaudeFraudExplanationService(HttpClient http, IConfiguration config, ILogger<ClaudeFraudExplanationService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> ExplainAsync(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts)
    {
        var apiKey = _config["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return $"[AI explanation unavailable — no API key configured] Flag type: {flag.FlagType}";
        }

        var prompt = BuildPrompt(flag, voucher, attempts);

        var requestBody = new
        {
            model = "claude-sonnet-4-6",
            max_tokens = 200,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Claude API call failed: {Status} {Body}", response.StatusCode, body);
                return $"[AI explanation unavailable] Flag type: {flag.FlagType}";
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return text ?? $"Flag type: {flag.FlagType}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API for fraud explanation");
            return $"[AI explanation unavailable] Flag type: {flag.FlagType}";
        }
    }

    private static string BuildPrompt(FraudFlag flag, Voucher voucher, List<RedemptionAttempt> attempts)
    {
        var attemptSummary = string.Join("\n", attempts.Select(a =>
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