using Microsoft.Extensions.Logging;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;
using StarHotel.Domain.Events;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.Messaging;
using StarHotel.Infrastructure.RealTime;

namespace StarHotel.Api.Services;

/// <summary>
/// Reservation service — implements BR-12 through BR-24
/// </summary>
public class ReservationService
{
    private readonly IBookingRepository _bookings;
    private readonly IRoomRepository _rooms;
    private readonly IEventPublisher _events;
    private readonly IDashboardNotifier _notifier;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        IBookingRepository bookings,
        IRoomRepository rooms,
        IEventPublisher events,
        IDashboardNotifier notifier,
        ILogger<ReservationService> logger)
    {
        _bookings = bookings;
        _rooms = rooms;
        _events = events;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// BR-24: Create a temporary booking record to reserve a booking ID
    /// </summary>
    public async Task<long> CreateTempBookingAsync(string createdBy, CancellationToken ct = default) =>
        await _bookings.CreateTempBookingAsync(createdBy, ct);

    /// <summary>
    /// BR-12: Save booking — transitions room status Open→Booked
    /// </summary>
    public async Task<Booking> SaveBookingAsync(SaveBookingRequest req, string userId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdAsync(req.BookingId, ct)
            ?? throw new InvalidOperationException($"Booking {req.BookingId} not found");

        var room = await _rooms.GetByIdAsync(req.RoomId, ct)
            ?? throw new InvalidOperationException($"Room {req.RoomId} not found");

        // BR-16: Maintenance rooms cannot be booked
        if (!room.CanBeBooked())
            throw new InvalidOperationException($"Room {req.RoomId} is under maintenance or inactive");

        // BR-18: SubTotal = StayDuration × RoomPrice
        var subTotal = req.StayDuration * room.RoomPrice;

        booking.GuestName = req.GuestName.Trim();
        booking.GuestPassport = req.GuestPassport.Trim();
        booking.GuestOrigin = req.GuestOrigin?.Trim() ?? string.Empty;
        booking.GuestContact = req.GuestContact?.Trim() ?? string.Empty;
        booking.GuestEmergencyContactName = req.GuestEmergencyContactName?.Trim() ?? string.Empty;
        booking.GuestEmergencyContactNo = req.GuestEmergencyContactNo?.Trim() ?? string.Empty;
        booking.TotalGuest = req.TotalGuest;
        booking.StayDuration = req.StayDuration;
        booking.BookingDate = req.BookingDate;
        booking.GuestCheckIn = req.GuestCheckIn;
        booking.GuestCheckOut = req.GuestCheckOut;
        booking.Remarks = req.Remarks?.Trim() ?? string.Empty;
        booking.RoomId = req.RoomId;
        booking.RoomNo = room.RoomShortName;
        booking.RoomType = room.RoomType;
        booking.RoomLocation = room.RoomLocation;
        booking.RoomPrice = room.RoomPrice;
        booking.Breakfast = room.Breakfast;
        booking.BreakfastPrice = room.BreakfastPrice;
        booking.SubTotal = subTotal;
        booking.Deposit = req.Deposit;
        booking.Payment = req.Payment;
        booking.LastModifiedDate = DateTime.UtcNow;
        booking.LastModifiedBy = userId;

        // BR-12: If status was Open, transition to Booked
        if (room.RoomStatus == RoomStatus.Open)
        {
            booking.CreatedDate = DateTime.UtcNow;
            booking.CreatedBy = userId;
        }

        booking.Temp = false;
        await _bookings.UpdateAsync(booking, ct);

        var newStatus = room.RoomStatus == RoomStatus.Open ? RoomStatus.Booked : room.RoomStatus;
        await _rooms.UpdateStatusAsync(req.RoomId, newStatus, booking.Id, userId, ct);

        // Emit event
        await _events.PublishAsync(new BookingCreatedEvent(
            booking.Id, booking.GuestName, req.RoomId, room.RoomShortName,
            booking.Payment, userId, DateTime.UtcNow), ServiceBusQueues.BookingEvents, ct);

        // Real-time push (BR-25)
        await _notifier.NotifyRoomStatusChangedAsync(req.RoomId, newStatus.ToString(), false, ct);
        await _notifier.NotifyRoomSummaryChangedAsync(await _rooms.GetSummaryAsync(ct), ct);

        _logger.LogInformation("Booking {BookingId} saved for room {RoomNo} by {User}", booking.Id, room.RoomShortName, userId);
        return booking;
    }

    /// <summary>
    /// BR-13: Check-In — validates full payment then transitions to Occupied
    /// </summary>
    public async Task<Booking> CheckInAsync(long bookingId, DateTime checkInTime, string userId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, ct)
            ?? throw new InvalidOperationException($"Booking {bookingId} not found");

        // BR-13: Payment must equal SubTotal + Deposit
        if (!await _bookings.IsPaidAsync(bookingId, ct))
            throw new InvalidOperationException("Full payment required before check-in");

        booking.GuestCheckIn = checkInTime;
        booking.LastModifiedDate = DateTime.UtcNow;
        booking.LastModifiedBy = userId;
        await _bookings.UpdateAsync(booking, ct);

        await _rooms.UpdateStatusAsync(booking.RoomId, RoomStatus.Occupied, booking.Id, userId, ct);

        await _events.PublishAsync(new CheckInEvent(
            bookingId, booking.RoomId, booking.RoomNo, checkInTime, userId, DateTime.UtcNow),
            ServiceBusQueues.BookingEvents, ct);

        await _notifier.NotifyRoomStatusChangedAsync(booking.RoomId, "Occupied", false, ct);
        await _notifier.NotifyRoomSummaryChangedAsync(await _rooms.GetSummaryAsync(ct), ct);

        _logger.LogInformation("Check-in completed for booking {BookingId} by {User}", bookingId, userId);
        return booking;
    }

    /// <summary>
    /// BR-14 / BR-15: Check-Out — enforces deposit refund rule, transitions to Housekeeping
    /// </summary>
    public async Task<Booking> CheckOutAsync(long bookingId, DateTime checkOutTime, decimal refund, string userId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, ct)
            ?? throw new InvalidOperationException($"Booking {bookingId} not found");

        // BR-14: Payment must be full
        if (!await _bookings.IsPaidAsync(bookingId, ct))
            throw new InvalidOperationException("Full payment required before check-out");

        // BR-14: After 2:00 PM check-out, deposit refund is forced to 0
        if (checkOutTime.TimeOfDay >= new TimeSpan(14, 0, 0))
        {
            refund = 0m;
            _logger.LogInformation("Booking {BookingId}: refund forced to 0 (check-out after 2 PM)", bookingId);
        }

        booking.GuestCheckOut = checkOutTime;
        booking.Refund = refund;
        booking.LastModifiedDate = DateTime.UtcNow;
        booking.LastModifiedBy = userId;
        await _bookings.UpdateAsync(booking, ct);

        // BR-15: Room transitions to Housekeeping after check-out
        await _rooms.UpdateStatusAsync(booking.RoomId, RoomStatus.Housekeeping, booking.Id, userId, ct);

        await _events.PublishAsync(new CheckOutEvent(
            bookingId, booking.RoomId, booking.RoomNo, checkOutTime, refund, userId, DateTime.UtcNow),
            ServiceBusQueues.BookingEvents, ct);

        await _notifier.NotifyRoomStatusChangedAsync(booking.RoomId, "Housekeeping", false, ct);
        await _notifier.NotifyRoomSummaryChangedAsync(await _rooms.GetSummaryAsync(ct), ct);

        _logger.LogInformation("Check-out completed for booking {BookingId} by {User}", bookingId, userId);
        return booking;
    }

    /// <summary>
    /// Void/unvoid a booking
    /// </summary>
    public async Task<Booking> VoidBookingAsync(long bookingId, string userId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, ct)
            ?? throw new InvalidOperationException($"Booking {bookingId} not found");

        if (booking.Temp)
            throw new InvalidOperationException("Cannot void a temporary booking");

        booking.Active = !booking.Active;
        booking.LastModifiedDate = DateTime.UtcNow;
        booking.LastModifiedBy = userId;
        await _bookings.UpdateAsync(booking, ct);

        await _events.PublishAsync(new BookingVoidedEvent(
            bookingId, !booking.Active, userId, DateTime.UtcNow),
            ServiceBusQueues.BookingEvents, ct);

        _logger.LogInformation("Booking {BookingId} {Action} by {User}", bookingId,
            booking.Active ? "unvoided" : "voided", userId);
        return booking;
    }
}

// DTO for save booking request
public record SaveBookingRequest(
    long BookingId,
    int RoomId,
    string GuestName,
    string GuestPassport,
    string? GuestOrigin,
    string? GuestContact,
    string? GuestEmergencyContactName,
    string? GuestEmergencyContactNo,
    int TotalGuest,
    int StayDuration,
    DateTime BookingDate,
    DateTime GuestCheckIn,
    DateTime GuestCheckOut,
    string? Remarks,
    decimal Deposit,
    decimal Payment);