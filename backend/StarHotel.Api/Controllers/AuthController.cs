using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StarHotel.Domain.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StarHotel.Api.Controllers;

/// <summary>
/// Auth controller — local JWT for dev/testing
/// In production, tokens are issued by Entra ID B2C; this endpoint is disabled
/// Replaces GoldFishEncode XOR cipher (BR-04) with proper JWT
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository users, IConfiguration config, ILogger<AuthController> logger)
    {
        _users = users;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/v1/auth/login — development/testing JWT login
    /// Production uses Entra ID token exchange
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "User ID and password are required" });

        // BR-01: User ID uppercased before lookup
        var userId = request.UserId.ToUpper();
        var user = await _users.GetByUserIdAsync(userId, ct);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for unknown user: {UserId}", userId);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // BR-03: Frozen account cannot log in
        if (!user.Active)
            return Unauthorized(new { error = "Account is locked. Please contact administrator." });

        // Development: accept any password for configured demo users
        // Production: validate against Entra ID
        var isDevMode = _config["ASPNETCORE_ENVIRONMENT"] == "Development";
        bool authenticated;

        if (isDevMode)
        {
            // Simple password check for demo (In prod, Entra ID handles this)
            authenticated = request.Password.Equals("admin", StringComparison.OrdinalIgnoreCase)
                            || request.Password.Equals("clerk", StringComparison.OrdinalIgnoreCase)
                            || request.Password == user.UserId.ToLower();
        }
        else
        {
            return BadRequest(new { error = "Use Entra ID authentication in production" });
        }

        if (!authenticated)
        {
            // BR-02 / BR-06: Increment attempts; admin is exempt
            if (user.UserGroup != 1)
            {
                user.LoginAttempts++;
                if (user.LoginAttempts > 2)
                {
                    user.Active = false; // BR-02: freeze after 3 failures
                    await _users.UpdateAsync(user, ct);
                    _logger.LogWarning("Account {UserId} frozen after {Attempts} failed attempts", userId, user.LoginAttempts);
                    return Unauthorized(new { error = "Account locked after too many failed attempts." });
                }
                await _users.UpdateAsync(user, ct);
            }
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Successful login — reset attempts
        user.LoginAttempts = 0;
        await _users.UpdateAsync(user, ct);

        var token = GenerateJwtToken(user);
        var groupName = user.UserGroup switch
        {
            1 => "Administrator",
            2 => "Manager",
            3 => "Supervisor",
            _ => "Clerk"
        };

        _logger.LogInformation("User {UserId} ({Group}) logged in", userId, groupName);

        return Ok(new
        {
            accessToken = token,
            userId = user.UserId,
            userName = user.UserName,
            userGroup = user.UserGroup,
            groupName,
            idle = user.Idle,             // BR-09/10
            dashboardBlink = user.DashboardBlink, // BR-25
            changePassword = user.ChangePassword, // BR-07
            expiresAt = DateTime.UtcNow.AddHours(8).ToString("O")
        });
    }

    private string GenerateJwtToken(Domain.Entities.UserData user)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var groupRole = user.UserGroup switch
        {
            1 => "Administrator",
            2 => "Manager",
            3 => "Supervisor",
            _ => "Clerk"
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.UserId),
            new Claim(ClaimTypes.GivenName, user.UserName),
            new Claim(ClaimTypes.Role, groupRole),
            new Claim("userGroup", user.UserGroup.ToString()),
            new Claim("idle", user.Idle.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string UserId, string Password);