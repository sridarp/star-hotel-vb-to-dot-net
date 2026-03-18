using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.RealTime;

namespace StarHotel.Api.Controllers;

/// <summary>
/// Room inventory API — manages room catalogue and status lifecycle (BR-11, BR-16, BR-17)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomRepository _rooms;
    private readonly IDashboardNotifier _notifier;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomRepository rooms,
        IDashboardNotifier notifier,
        ILogger<RoomsController> logger)
    {
        _rooms = rooms;
        _notifier = notifier;
        _logger = logger;
    }

    private string CurrentUserId => User.Identity?.Name?.ToUpper() ?? "SYSTEM";

    /// <summary>
    /// GET /api/v1/rooms — all active rooms with status (for dashboard grid)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "DashboardRead")]
    public async Task<IActionResult> GetRooms(CancellationToken ct)
    {
        var rooms = await _rooms.GetAllActiveAsync(ct);
        return Ok(rooms.Select(MapToDto));
    }

    /// <summary>
    /// GET /api/v1/rooms/summary — room status summary counts
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = "DashboardRead")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _rooms.GetSummaryAsync(ct);
        return Ok(summary);
    }

    /// <summary>
    /// GET /api/v1/rooms/{id} — single room details
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "DashboardRead")]
    public async Task<IActionResult> GetRoom(int id, CancellationToken ct)
    {
        var room = await _rooms.GetByIdAsync(id, ct);
        if (room == null) return NotFound(new { error = $"Room {id} not found" });
        return Ok(MapToDto(room));
    }

    /// <summary>
    /// POST /api/v1/rooms — create room (Admin/Manager only) (BR-17)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RoomMaintain")]
    public async Task<IActionResult> CreateRoom([FromBody] UpsertRoomDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var room = new Room
        {
            Id = dto.Id,
            RoomShortName = dto.RoomShortName,
            RoomLongName = dto.RoomLongName ?? string.Empty,
            RoomStatus = RoomStatus.Open,
            RoomType = dto.RoomType,
            RoomLocation = dto.RoomLocation,
            RoomPrice = dto.RoomPrice,
            Breakfast = dto.Breakfast,
            BreakfastPrice = dto.BreakfastPrice,
            Maintenance = false,
            Active = true,
            CreatedBy = CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };

        await _rooms.AddAsync(room, ct);
        _logger.LogInformation("Room {RoomNo} created by {User}", dto.RoomShortName, CurrentUserId);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, MapToDto(room));
    }

    /// <summary>
    /// PATCH /api/v1/rooms/{id}/status — change room status (BR-11)
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeRoomStatusDto dto, CancellationToken ct)
    {
        var room = await _rooms.GetByIdAsync(id, ct);
        if (room == null) return NotFound(new { error = $"Room {id} not found" });

        if (!Enum.TryParse<RoomStatus>(dto.Status, true, out var newStatus))
            return BadRequest(new { error = $"Invalid status: {dto.Status}" });

        // BR-11: enforce valid transitions
        if (!room.CanTransitionTo(newStatus))
            return BadRequest(new { error = $"Cannot transition from {room.RoomStatus} to {newStatus}" });

        await _rooms.UpdateStatusAsync(id, newStatus, 0, CurrentUserId, ct);

        // Real-time push (BR-25)
        await _notifier.NotifyRoomStatusChangedAsync(id, newStatus.ToString(), false, ct);
        await _notifier.NotifyRoomSummaryChangedAsync(await _rooms.GetSummaryAsync(ct), ct);

        return Ok(new { roomId = id, status = newStatus.ToString() });
    }

    /// <summary>
    /// GET /api/v1/rooms/types — room type catalogue
    /// </summary>
    [HttpGet("types")]
    [Authorize(Policy = "DashboardRead")]
    public async Task<IActionResult> GetRoomTypes(CancellationToken ct)
    {
        var types = await _rooms.GetRoomTypesAsync(ct);
        return Ok(types);
    }

    private static RoomDto MapToDto(Room r) => new(
        r.Id, r.RoomShortName, r.RoomLongName,
        r.RoomStatus.ToString(), r.RoomType, r.RoomLocation,
        r.RoomPrice, r.Breakfast, r.BreakfastPrice,
        r.Maintenance, r.Active, r.BookingId);
}

// DTOs
public record RoomDto(
    int Id, string RoomShortName, string RoomLongName,
    string RoomStatus, string RoomType, string RoomLocation,
    decimal RoomPrice, bool Breakfast, decimal BreakfastPrice,
    bool Maintenance, bool Active, long BookingId);

public record UpsertRoomDto(
    int Id, string RoomShortName, string? RoomLongName,
    string RoomType, string RoomLocation,
    decimal RoomPrice, bool Breakfast, decimal BreakfastPrice);

public record ChangeRoomStatusDto(string Status);