using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.Persistence;

namespace StarHotel.Api.Controllers;

/// <summary>
/// Reporting API — BR-26 through BR-29
/// Replaces Crystal Reports with server-side SQL queries
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly StarHotelDbContext _ctx;
    private readonly IUserRepository _users;
    private readonly ICompanyRepository _company;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        StarHotelDbContext ctx,
        IUserRepository users,
        ICompanyRepository company,
        ILogger<ReportsController> logger)
    {
        _ctx = ctx;
        _users = users;
        _company = company;
        _logger = logger;
    }

    private string CurrentUserId => User.Identity?.Name?.ToUpper() ?? "SYSTEM";

    /// <summary>
    /// GET /api/v1/reports/daily?date=2024-01-01 — Daily Booking Report (BR-27, ModuleId=12)
    /// </summary>
    [HttpGet("daily")]
    [Authorize(Policy = "ReportList")]
    public async Task<IActionResult> DailyReport([FromQuery] DateTime? date, CancellationToken ct)
    {
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 12, ct))
            return Forbid();

        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        var data = await _ctx.Bookings
            .Where(b => b.Active && !b.Temp && b.CreatedDate.Date == targetDate)
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.BookingDate, b.GuestCheckIn, b.GuestCheckOut,
                b.GuestName, b.RoomNo, b.RoomType,
                b.Deposit, b.Payment, b.CreatedDate, b.CreatedBy
            })
            .ToListAsync(ct);

        return Ok(new { reportDate = targetDate, records = data, count = data.Count });
    }

    /// <summary>
    /// GET /api/v1/reports/shift?userId=ADMIN&date=2024-01-01 — Shift Report by Staff (ModuleId=16)
    /// </summary>
    [HttpGet("shift")]
    [Authorize(Policy = "ReportList")]
    public async Task<IActionResult> ShiftReport([FromQuery] string? userId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetUser = userId?.ToUpper() ?? CurrentUserId;

        // BR-28: Shift report for specific user requires module 16 access
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 16, ct))
            return Forbid();

        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        var data = await _ctx.Bookings
            .Where(b => b.Active && !b.Temp
                        && b.CreatedBy == targetUser
                        && b.CreatedDate.Date == targetDate)
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.BookingDate, b.GuestCheckIn, b.GuestCheckOut,
                b.GuestName, b.RoomNo, b.RoomType,
                b.Deposit, b.Payment,
                Total = b.Payment - b.Refund,
                b.CreatedDate, b.CreatedBy
            })
            .ToListAsync(ct);

        return Ok(new { reportDate = targetDate, userId = targetUser, records = data, count = data.Count });
    }

    /// <summary>
    /// GET /api/v1/reports/shift/all?date=2024-01-01 — Shift Report All Staff (ModuleId=17)
    /// </summary>
    [HttpGet("shift/all")]
    [Authorize(Policy = "ReportList")]
    public async Task<IActionResult> ShiftReportAllStaff([FromQuery] DateTime? date, CancellationToken ct)
    {
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 17, ct))
            return Forbid();

        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        var data = await _ctx.Bookings
            .Where(b => b.Active && !b.Temp && b.CreatedDate.Date == targetDate)
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.BookingDate, b.GuestCheckIn, b.GuestCheckOut,
                b.GuestName, b.RoomNo, b.RoomType,
                b.Deposit, b.Payment, b.CreatedDate, b.CreatedBy
            })
            .ToListAsync(ct);

        return Ok(new { reportDate = targetDate, records = data, count = data.Count });
    }

    /// <summary>
    /// GET /api/v1/reports/weekly?startDate=2024-01-01 — Weekly Booking Report (ModuleId=13)
    /// </summary>
    [HttpGet("weekly")]
    [Authorize(Policy = "ReportList")]
    public async Task<IActionResult> WeeklyReport([FromQuery] DateTime? startDate, CancellationToken ct)
    {
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 13, ct))
            return Forbid();

        var start = startDate?.Date ?? DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek).Date;
        var end = start.AddDays(7);

        var data = await _ctx.Bookings
            .Where(b => b.Active && !b.Temp && b.CreatedDate.Date >= start && b.CreatedDate.Date < end)
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.BookingDate, b.GuestCheckIn, b.GuestCheckOut,
                b.GuestName, b.RoomNo, b.RoomType,
                b.Deposit, b.Payment, b.CreatedDate, b.CreatedBy
            })
            .ToListAsync(ct);

        return Ok(new { startDate = start, endDate = end, records = data, count = data.Count });
    }

    /// <summary>
    /// GET /api/v1/reports/monthly?year=2024&month=1 — Monthly Booking Report (ModuleId=14)
    /// </summary>
    [HttpGet("monthly")]
    [Authorize(Policy = "ReportList")]
    public async Task<IActionResult> MonthlyReport([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 14, ct))
            return Forbid();

        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;

        var data = await _ctx.Bookings
            .Where(b => b.Active && !b.Temp
                        && b.CreatedDate.Year == y && b.CreatedDate.Month == m)
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.BookingDate, b.GuestCheckIn, b.GuestCheckOut,
                b.GuestName, b.RoomNo, b.RoomType,
                b.Deposit, b.Payment, b.CreatedDate, b.CreatedBy
            })
            .ToListAsync(ct);

        return Ok(new { year = y, month = m, records = data, count = data.Count });
    }

    /// <summary>
    /// GET /api/v1/reports/customers?name=John — customer search (ModuleId=8)
    /// </summary>
    [HttpGet("customers")]
    [Authorize(Policy = "FindCustomer")]
    public async Task<IActionResult> FindCustomers([FromQuery] string? name, [FromQuery] string? passport, CancellationToken ct)
    {
        if (!await _users.HasModuleAccessAsync(CurrentUserId, 8, ct))
            return Forbid();

        var query = _ctx.Bookings.Where(b => b.Active && !b.Temp).AsQueryable();

        if (!string.IsNullOrEmpty(name))
            query = query.Where(b => b.GuestName.Contains(name));

        if (!string.IsNullOrEmpty(passport))
            query = query.Where(b => b.GuestPassport.Contains(passport));

        var results = await query
            .OrderByDescending(b => b.CreatedDate)
            .Select(b => new
            {
                bookingId = b.FormattedId,
                b.GuestName, b.GuestPassport, b.GuestOrigin,
                b.GuestContact, b.RoomNo, b.RoomType,
                b.GuestCheckIn, b.GuestCheckOut,
                b.Payment, b.CreatedDate, b.CreatedBy
            })
            .Take(100)
            .ToListAsync(ct);

        return Ok(new { results, count = results.Count });
    }
}