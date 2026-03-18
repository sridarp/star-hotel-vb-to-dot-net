using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarHotel.Api.Services;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Interfaces;

namespace StarHotel.Api.Controllers;

/// <summary>
/// Booking API — implements BR-12 through BR-24
/// All endpoints require authentication; RBAC enforced via policy
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly ReservationService _reservation;
    private readonly IBookingRepository _bookings;
    private readonly PricingService _pricing;
    private readonly DocumentService _documents;
    private readonly ICompanyRepository _company;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        ReservationService reservation,
        IBookingRepository bookings,
        PricingService pricing,
        DocumentService documents,
        ICompanyRepository company,
        ILogger<BookingsController> logger)
    {
        _reservation = reservation;
        _bookings = bookings;
        _pricing = pricing;
        _documents = documents;
        _company = company;
        _logger = logger;
    }

    private string CurrentUserId => User.Identity?.Name?.ToUpper() ?? "SYSTEM";

    /// <summary>
    /// GET /api/v1/bookings — list active bookings (Clerk+)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "BookingRead")]
    public async Task<IActionResult> GetBookings(CancellationToken ct)
    {
        var bookings = await _bookings.GetActiveBookingsAsync(ct);
        var result = bookings.Select(b => MapToDto(b));
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/bookings/{id} — get booking details
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = "BookingRead")]
    public async Task<IActionResult> GetBooking(long id, CancellationToken ct)
    {
        var booking = await _bookings.GetByIdAsync(id, ct);
        if (booking == null) return NotFound(new { error = $"Booking {id} not found" });
        return Ok(MapToDto(booking));
    }

    /// <summary>
    /// POST /api/v1/bookings/temp — create temp booking (BR-24)
    /// </summary>
    [HttpPost("temp")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> CreateTempBooking(CancellationToken ct)
    {
        var id = await _reservation.CreateTempBookingAsync(CurrentUserId, ct);
        return Ok(new { bookingId = id, formattedId = id.ToString("D6") });
    }

    /// <summary>
    /// PUT /api/v1/bookings/{id} — save booking (BR-12)
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> SaveBooking(long id, [FromBody] SaveBookingDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.BookingId) return BadRequest(new { error = "ID mismatch" });

        try
        {
            var request = new SaveBookingRequest(
                dto.BookingId, dto.RoomId, dto.GuestName, dto.GuestPassport,
                dto.GuestOrigin, dto.GuestContact, dto.GuestEmergencyContactName,
                dto.GuestEmergencyContactNo, dto.TotalGuest, dto.StayDuration,
                dto.BookingDate, dto.GuestCheckIn, dto.GuestCheckOut,
                dto.Remarks, dto.Deposit, dto.Payment);

            var booking = await _reservation.SaveBookingAsync(request, CurrentUserId, ct);
            return Ok(MapToDto(booking));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/v1/bookings/{id}/checkin — check-in (BR-13)
    /// </summary>
    [HttpPost("{id:long}/checkin")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> CheckIn(long id, [FromBody] CheckInDto dto, CancellationToken ct)
    {
        try
        {
            var booking = await _reservation.CheckInAsync(id, dto.CheckInTime, CurrentUserId, ct);
            return Ok(MapToDto(booking));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/v1/bookings/{id}/checkout — check-out (BR-14/BR-15)
    /// </summary>
    [HttpPost("{id:long}/checkout")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> CheckOut(long id, [FromBody] CheckOutDto dto, CancellationToken ct)
    {
        try
        {
            var booking = await _reservation.CheckOutAsync(id, dto.CheckOutTime, dto.Refund, CurrentUserId, ct);
            return Ok(MapToDto(booking));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/v1/bookings/{id}/void — void/unvoid booking
    /// </summary>
    [HttpPost("{id:long}/void")]
    [Authorize(Policy = "BookingWrite")]
    public async Task<IActionResult> VoidBooking(long id, CancellationToken ct)
    {
        try
        {
            var booking = await _reservation.VoidBookingAsync(id, CurrentUserId, ct);
            return Ok(new { bookingId = id, active = booking.Active });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/v1/bookings/{id}/receipt/temporary — Temporary Receipt PDF (BR-21)
    /// </summary>
    [HttpGet("{id:long}/receipt/temporary")]
    [Authorize(Policy = "BookingRead")]
    public async Task<IActionResult> GetTemporaryReceipt(long id, CancellationToken ct)
    {
        var booking = await _bookings.GetByIdAsync(id, ct);
        if (booking == null) return NotFound();

        var company = await _company.GetAsync(ct);
        if (company == null) return StatusCode(500, new { error = "Company settings not found" });

        var pdf = _documents.GenerateTemporaryReceipt(booking, company);
        return File(pdf, "application/pdf", $"TR-{booking.FormattedId}.pdf");
    }

    /// <summary>
    /// GET /api/v1/bookings/{id}/receipt/official — Official Receipt PDF (BR-22)
    /// </summary>
    [HttpGet("{id:long}/receipt/official")]
    [Authorize(Policy = "BookingRead")]
    public async Task<IActionResult> GetOfficialReceipt(long id, CancellationToken ct)
    {
        var booking = await _bookings.GetByIdAsync(id, ct);
        if (booking == null) return NotFound();

        var company = await _company.GetAsync(ct);
        if (company == null) return StatusCode(500, new { error = "Company settings not found" });

        var pdf = _documents.GenerateOfficialReceipt(booking, company);
        return File(pdf, "application/pdf", $"OR-{booking.FormattedId}.pdf");
    }

    /// <summary>
    /// POST /api/v1/bookings/calculate — pricing calculation (BR-18/BR-19/BR-23)
    /// </summary>
    [HttpPost("calculate")]
    [Authorize(Policy = "BookingRead")]
    public IActionResult Calculate([FromBody] PriceCalculationDto dto)
    {
        var subTotal = _pricing.CalculateSubTotal(dto.StayDuration, dto.RoomPrice);
        var totalDue = _pricing.CalculateTotalDue(subTotal, dto.Deposit);
        var checkOutDate = _pricing.CalculateCheckOutDate(dto.CheckIn, dto.StayDuration);
        var refund = _pricing.CalculateRefund(dto.CheckOut, dto.Deposit);

        return Ok(new
        {
            subTotal,
            totalDue,
            checkOutDate,
            refund,
            defaultDeposit = _pricing.DefaultDepositAmount()
        });
    }

    private static BookingDto MapToDto(Booking b) => new(
        b.Id, b.FormattedId,
        b.GuestName, b.GuestPassport, b.GuestOrigin, b.GuestContact,
        b.GuestEmergencyContactName, b.GuestEmergencyContactNo,
        b.TotalGuest, b.StayDuration,
        b.BookingDate, b.GuestCheckIn, b.GuestCheckOut, b.Remarks,
        b.RoomId, b.RoomNo, b.RoomType, b.RoomLocation, b.RoomPrice,
        b.Breakfast, b.BreakfastPrice,
        b.SubTotal, b.Deposit, b.Payment, b.Refund, b.TotalDue,
        b.Active, b.Temp,
        b.CreatedDate, b.CreatedBy, b.LastModifiedDate, b.LastModifiedBy);
}

// DTOs
public record BookingDto(
    long Id, string FormattedId,
    string GuestName, string GuestPassport, string? GuestOrigin, string? GuestContact,
    string? GuestEmergencyContactName, string? GuestEmergencyContactNo,
    int TotalGuest, int StayDuration,
    DateTime BookingDate, DateTime GuestCheckIn, DateTime GuestCheckOut, string? Remarks,
    int RoomId, string RoomNo, string RoomType, string RoomLocation, decimal RoomPrice,
    bool Breakfast, decimal BreakfastPrice,
    decimal SubTotal, decimal Deposit, decimal Payment, decimal Refund, decimal TotalDue,
    bool Active, bool Temp,
    DateTime CreatedDate, string CreatedBy, DateTime? LastModifiedDate, string LastModifiedBy);

public record SaveBookingDto(
    long BookingId, int RoomId,
    string GuestName, string GuestPassport,
    string? GuestOrigin, string? GuestContact,
    string? GuestEmergencyContactName, string? GuestEmergencyContactNo,
    int TotalGuest, int StayDuration,
    DateTime BookingDate, DateTime GuestCheckIn, DateTime GuestCheckOut,
    string? Remarks, decimal Deposit, decimal Payment);

public record CheckInDto(DateTime CheckInTime);
public record CheckOutDto(DateTime CheckOutTime, decimal Refund);
public record PriceCalculationDto(int StayDuration, decimal RoomPrice, decimal Deposit, DateTime CheckIn, DateTime CheckOut);