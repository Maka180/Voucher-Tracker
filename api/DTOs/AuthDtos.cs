namespace VoucherTracker.Api.DTOs;

public record RegisterRequest(string FullName, string Phone, string Password);
public record LoginRequest(string Phone, string Password);
public record AuthResponse(string Token, string FullName, string Role);