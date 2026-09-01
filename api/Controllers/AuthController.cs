using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoucherTracker.Api.Data;
using VoucherTracker.Api.DTOs;
using VoucherTracker.Api.Models;
using VoucherTracker.Api.Services;

namespace VoucherTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly AuditService _audit;

    public AuthController(AppDbContext db, TokenService tokenService,  AuditService audit)
    {
        _db = db;
        _tokenService = tokenService;
        _audit = audit;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Phone == request.Phone))
            return BadRequest("A user with this phone number already exists.");

        var user = new User
        {
            FullName = request.FullName,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Sender"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);
        await _audit.LogAsync(user.Id, "Register", "User", user.Id);
        return Ok(new AuthResponse(token, user.FullName, user.Role));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid phone number or password.");

        var token = _tokenService.CreateToken(user);
        await _audit.LogAsync(user.Id, "Login", "User", user.Id);
        return Ok(new AuthResponse(token, user.FullName, user.Role));
    }
}