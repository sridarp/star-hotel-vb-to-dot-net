using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Interfaces;

namespace StarHotel.Api.Controllers;

/// <summary>
/// User management API — BR-01 through BR-10
/// Replaces VB6 frmUserMaintain + modFunction.bas UserAccessModule
/// Note: password management delegated to Entra ID; these endpoints manage UserData metadata
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository users, ILogger<UsersController> logger)
    {
        _users = users;
        _logger = logger;
    }

    private string CurrentUserId => User.Identity?.Name?.ToUpper() ?? "SYSTEM";

    /// <summary>
    /// GET /api/v1/users/me — current user profile + module access
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var user = await _users.GetByUserIdAsync(userId, ct);
        if (user == null) return NotFound(new { error = $"User {userId} not found" });

        return Ok(MapToDto(user));
    }

    /// <summary>
    /// GET /api/v1/users/me/access/{moduleId} — check module access (BR-05)
    /// </summary>
    [HttpGet("me/access/{moduleId:int}")]
    public async Task<IActionResult> CheckAccess(int moduleId, CancellationToken ct)
    {
        var hasAccess = await _users.HasModuleAccessAsync(CurrentUserId, moduleId, ct);
        return Ok(new { moduleId, hasAccess });
    }

    /// <summary>
    /// GET /api/v1/users — list all users (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "UserMaintain")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _users.GetAllAsync(ct);
        return Ok(users.Select(MapToDto));
    }

    /// <summary>
    /// GET /api/v1/users/{userId} — get user by ID (Admin only)
    /// </summary>
    [HttpGet("{userId}")]
    [Authorize(Policy = "UserMaintain")]
    public async Task<IActionResult> GetUser(string userId, CancellationToken ct)
    {
        var user = await _users.GetByUserIdAsync(userId.ToUpper(), ct);
        if (user == null) return NotFound(new { error = $"User {userId} not found" });
        return Ok(MapToDto(user));
    }

    /// <summary>
    /// POST /api/v1/users — create user (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "UserMaintain")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // BR-01: User ID is uppercase
        var newUser = new UserData
        {
            UserId = dto.UserId.ToUpper(),
            UserName = dto.UserName,
            UserGroup = dto.UserGroup,
            Idle = Math.Clamp(dto.Idle, 0, 3600), // BR-10: clamp idle to 0-3600
            LoginAttempts = 0,
            ChangePassword = true, // BR-07: force change on first login
            DashboardBlink = dto.DashboardBlink,
            Active = true
        };

        await _users.AddAsync(newUser, ct);
        _logger.LogInformation("User {UserId} created by {Creator}", newUser.UserId, CurrentUserId);
        return CreatedAtAction(nameof(GetUser), new { userId = newUser.UserId }, MapToDto(newUser));
    }

    /// <summary>
    /// PATCH /api/v1/users/{userId} — update user settings
    /// </summary>
    [HttpPatch("{userId}")]
    [Authorize(Policy = "UserMaintain")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        var user = await _users.GetByUserIdAsync(userId.ToUpper(), ct);
        if (user == null) return NotFound();

        if (dto.UserGroup.HasValue) user.UserGroup = dto.UserGroup.Value;
        if (dto.Idle.HasValue) user.Idle = Math.Clamp(dto.Idle.Value, 0, 3600); // BR-10
        if (dto.DashboardBlink.HasValue) user.DashboardBlink = dto.DashboardBlink.Value;
        if (dto.Active.HasValue) user.Active = dto.Active.Value;
        if (dto.ChangePassword.HasValue) user.ChangePassword = dto.ChangePassword.Value;

        await _users.UpdateAsync(user, ct);
        return Ok(MapToDto(user));
    }

    /// <summary>
    /// PATCH /api/v1/users/{userId}/unlock — reset login attempts (Admin)
    /// Replaces BR-02 lockout management
    /// </summary>
    [HttpPatch("{userId}/unlock")]
    [Authorize(Policy = "UserMaintain")]
    public async Task<IActionResult> UnlockUser(string userId, CancellationToken ct)
    {
        var user = await _users.GetByUserIdAsync(userId.ToUpper(), ct);
        if (user == null) return NotFound();

        user.LoginAttempts = 0;
        user.Active = true; // BR-03: reactivate frozen account
        await _users.UpdateAsync(user, ct);

        _logger.LogInformation("User {UserId} unlocked by {Admin}", userId, CurrentUserId);
        return Ok(new { userId, message = "Account unlocked" });
    }

    private static UserDto MapToDto(UserData u) => new(
        u.Id, u.UserId, u.UserName, u.UserGroup,
        u.Idle, u.LoginAttempts, u.ChangePassword, u.DashboardBlink, u.Active);
}

// DTOs
public record UserDto(
    int Id, string UserId, string UserName, int UserGroup,
    int Idle, int LoginAttempts, bool ChangePassword, bool DashboardBlink, bool Active);

public record CreateUserDto(
    string UserId, string UserName, int UserGroup,
    int Idle = 0, bool DashboardBlink = true);

public record UpdateUserDto(
    int? UserGroup, int? Idle, bool? DashboardBlink, bool? Active, bool? ChangePassword);