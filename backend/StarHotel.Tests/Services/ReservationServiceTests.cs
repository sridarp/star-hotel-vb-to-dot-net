using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using StarHotel.Api.Services;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;
using StarHotel.Domain.Events;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.Messaging;
using StarHotel.Infrastructure.RealTime;
using Xunit;

namespace StarHotel.Tests.Services;

/// <summary>
/// BDD tests for ReservationService — maps to Stage 2 BDD scenarios F5, F6, F7
/// </summary>
public class ReservationServiceTests
{
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IEventPublisher> _events = new();
    private readonly Mock<IDashboardNotifier> _notifier = new();
    private readonly Mock<ILogger<ReservationService>> _logger = new();

    private ReservationService CreateSut() => new(
        _bookings.Object, _rooms.Object, _events.Object, _notifier.Object, _logger.Object);

    // ── BR-12: Open→Booked on save ────────────────────────────────────────────

    [Fact]
    public async Task SaveBooking_WhenRoomIsOpen_TransitionsToBooked()
    {
        // Arrange
        var booking = new Booking { Id = 1, Temp = true, Active = true, CreatedBy = "ADMIN" };
        var room = new Room { Id = 1, RoomShortName = "101", RoomType = "SINGLE BED ROOM", RoomStatus = RoomStatus.Open, RoomPrice = 100m, Active = true };
        var req = new SaveBookingRequest(1, 1, "John Doe", "A1234567", null, null, null, null, 2, 3,
            DateTime.Today, DateTime.Today.AddDays(1), DateTime.Today.AddDays(4), null, 20m, 320m);

        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _rooms.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(room);
        _bookings.Setup(r => r.UpdateAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RoomStatus>(), It.IsAny<long>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _events.Setup(e => e.PublishAsync(It.IsAny<BookingCreatedEvent>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.GetSummaryAsync(default)).ReturnsAsync(new RoomSummary(0, 1, 0, 0, 0));
        _notifier.Setup(n => n.NotifyRoomStatusChangedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), default)).Returns(Task.CompletedTask);
        _notifier.Setup(n => n.NotifyRoomSummaryChangedAsync(It.IsAny<object>(), default)).Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().SaveBookingAsync(req, "ADMIN");

        // Assert
        _rooms.Verify(r => r.UpdateStatusAsync(1, RoomStatus.Booked, 1, "ADMIN", default), Times.Once);
        result.Temp.Should().BeFalse();
        result.GuestName.Should().Be("John Doe");
    }

    // ── BR-18: SubTotal = StayDuration × RoomPrice ────────────────────────────

    [Fact]
    public async Task SaveBooking_CalculatesSubTotalCorrectly()
    {
        // Arrange: 3 nights × MYR 100 = MYR 300
        var booking = new Booking { Id = 1, Temp = true, Active = true, CreatedBy = "ADMIN" };
        var room = new Room { Id = 1, RoomShortName = "101", RoomPrice = 100m, RoomStatus = RoomStatus.Open, Active = true };
        var req = new SaveBookingRequest(1, 1, "Jane Doe", "B9876543", null, null, null, null,
            1, 3, DateTime.Today, DateTime.Today.AddDays(1), DateTime.Today.AddDays(4),
            null, 20m, 320m);

        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _rooms.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(room);
        _bookings.Setup(r => r.UpdateAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RoomStatus>(), It.IsAny<long>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _events.Setup(e => e.PublishAsync(It.IsAny<BookingCreatedEvent>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.GetSummaryAsync(default)).ReturnsAsync(new RoomSummary(0, 1, 0, 0, 0));
        _notifier.Setup(n => n.NotifyRoomStatusChangedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), default)).Returns(Task.CompletedTask);
        _notifier.Setup(n => n.NotifyRoomSummaryChangedAsync(It.IsAny<object>(), default)).Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().SaveBookingAsync(req, "ADMIN");

        // Assert — BR-18
        result.SubTotal.Should().Be(300m);
    }

    // ── BR-16: Maintenance rooms cannot be booked ─────────────────────────────

    [Fact]
    public async Task SaveBooking_WhenRoomIsInMaintenance_ThrowsException()
    {
        var booking = new Booking { Id = 1, Temp = true, Active = true };
        var room = new Room { Id = 1, RoomStatus = RoomStatus.Maintenance, Active = true };
        var req = new SaveBookingRequest(1, 1, "Test", "X123", null, null, null, null, 1, 1,
            DateTime.Today, DateTime.Today.AddDays(1), DateTime.Today.AddDays(2), null, 20m, 120m);

        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _rooms.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(room);

        // Act & Assert — BR-16
        var act = async () => await CreateSut().SaveBookingAsync(req, "ADMIN");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maintenance*");
    }

    // ── BR-13: Check-In requires full payment ─────────────────────────────────

    [Fact]
    public async Task CheckIn_WhenNotPaid_ThrowsException()
    {
        var booking = new Booking { Id = 1, SubTotal = 300m, Deposit = 20m, Payment = 100m };
        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _bookings.Setup(r => r.IsPaidAsync(1, default)).ReturnsAsync(false);

        var act = async () => await CreateSut().CheckInAsync(1, DateTime.Now, "CLERK");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*payment*");
    }

    [Fact]
    public async Task CheckIn_WhenFullyPaid_TransitionsToOccupied()
    {
        var booking = new Booking { Id = 1, RoomId = 1, RoomNo = "101", SubTotal = 300m, Deposit = 20m, Payment = 320m };
        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _bookings.Setup(r => r.IsPaidAsync(1, default)).ReturnsAsync(true);
        _bookings.Setup(r => r.UpdateAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.UpdateStatusAsync(1, RoomStatus.Occupied, 1, "CLERK", default)).Returns(Task.CompletedTask);
        _events.Setup(e => e.PublishAsync(It.IsAny<CheckInEvent>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.GetSummaryAsync(default)).ReturnsAsync(new RoomSummary(0, 0, 1, 0, 0));
        _notifier.Setup(n => n.NotifyRoomStatusChangedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), default)).Returns(Task.CompletedTask);
        _notifier.Setup(n => n.NotifyRoomSummaryChangedAsync(It.IsAny<object>(), default)).Returns(Task.CompletedTask);

        var result = await CreateSut().CheckInAsync(1, DateTime.Now, "CLERK");

        _rooms.Verify(r => r.UpdateStatusAsync(1, RoomStatus.Occupied, 1, "CLERK", default), Times.Once);
        result.Should().NotBeNull();
    }

    // ── BR-14: After 2PM checkout — deposit no refund ────────────────────────

    [Fact]
    public async Task CheckOut_After2PM_ForcesRefundToZero()
    {
        var booking = new Booking { Id = 1, RoomId = 1, RoomNo = "101", SubTotal = 300m, Deposit = 20m, Payment = 320m };
        var checkOutTime = DateTime.Today.AddHours(14).AddMinutes(30); // 2:30 PM

        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _bookings.Setup(r => r.IsPaidAsync(1, default)).ReturnsAsync(true);
        _bookings.Setup(r => r.UpdateAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.UpdateStatusAsync(1, RoomStatus.Housekeeping, 1, "CLERK", default)).Returns(Task.CompletedTask);
        _events.Setup(e => e.PublishAsync(It.IsAny<CheckOutEvent>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.GetSummaryAsync(default)).ReturnsAsync(new RoomSummary(0, 0, 0, 1, 0));
        _notifier.Setup(n => n.NotifyRoomStatusChangedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), default)).Returns(Task.CompletedTask);
        _notifier.Setup(n => n.NotifyRoomSummaryChangedAsync(It.IsAny<object>(), default)).Returns(Task.CompletedTask);

        // Act: request refund=20 but checkout after 2PM so should be forced to 0
        var result = await CreateSut().CheckOutAsync(1, checkOutTime, 20m, "CLERK");

        // Assert — BR-14: refund forced to 0
        result.Refund.Should().Be(0m);
        _rooms.Verify(r => r.UpdateStatusAsync(1, RoomStatus.Housekeeping, It.IsAny<long>(), "CLERK", default), Times.Once);
    }

    // ── BR-15: After check-out, status → Housekeeping ────────────────────────

    [Fact]
    public async Task CheckOut_Before2PM_KeepsRequestedRefund()
    {
        var booking = new Booking { Id = 1, RoomId = 1, RoomNo = "101", SubTotal = 300m, Deposit = 20m, Payment = 320m };
        var checkOutTime = DateTime.Today.AddHours(11); // 11 AM — before cutoff

        _bookings.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(booking);
        _bookings.Setup(r => r.IsPaidAsync(1, default)).ReturnsAsync(true);
        _bookings.Setup(r => r.UpdateAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RoomStatus>(), It.IsAny<long>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _events.Setup(e => e.PublishAsync(It.IsAny<CheckOutEvent>(), It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _rooms.Setup(r => r.GetSummaryAsync(default)).ReturnsAsync(new RoomSummary(0, 0, 0, 1, 0));
        _notifier.Setup(n => n.NotifyRoomStatusChangedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), default)).Returns(Task.CompletedTask);
        _notifier.Setup(n => n.NotifyRoomSummaryChangedAsync(It.IsAny<object>(), default)).Returns(Task.CompletedTask);

        var result = await CreateSut().CheckOutAsync(1, checkOutTime, 20m, "CLERK");

        result.Refund.Should().Be(20m);
    }
}

/// <summary>
/// BDD tests for PricingService — maps to Stage 2 BDD scenarios F5 pricing
/// </summary>
public class PricingServiceTests
{
    private readonly PricingService _sut = new();

    // BR-18: SubTotal = StayDuration × RoomPrice
    [Theory]
    [InlineData(1, 100, 100)]
    [InlineData(3, 100, 300)]
    [InlineData(7, 85.50, 598.50)]
    public void CalculateSubTotal_ReturnsCorrectValue(int nights, decimal price, decimal expected)
    {
        _sut.CalculateSubTotal(nights, price).Should().Be(expected);
    }

    // BR-19: TotalDue = SubTotal + Deposit
    [Fact]
    public void CalculateTotalDue_ReturnsSubTotalPlusDeposit()
    {
        _sut.CalculateTotalDue(300m, 20m).Should().Be(320m);
    }

    // BR-20: Default deposit = 20.00
    [Fact]
    public void DefaultDeposit_Is20()
    {
        _sut.DefaultDepositAmount().Should().Be(20m);
    }

    // BR-23: Check-out time at 12:00 PM; duration logic depends on check-in time
    [Fact]
    public void CalculateCheckOutDate_WhenCheckInAfterNoon_AddsFullDuration()
    {
        var checkIn = DateTime.Today.AddHours(12); // 12:00 PM exactly
        var result = _sut.CalculateCheckOutDate(checkIn, 3);
        result.Should().Be(checkIn.Date.AddDays(3).AddHours(12));
    }

    [Fact]
    public void CalculateCheckOutDate_WhenCheckInBeforeNoon_SubtractOneDay()
    {
        var checkIn = DateTime.Today.AddHours(9); // 9:00 AM
        var result = _sut.CalculateCheckOutDate(checkIn, 3);
        result.Should().Be(checkIn.Date.AddDays(2).AddHours(12));
    }

    // BR-14: Refund=0 after 2PM
    [Fact]
    public void CalculateRefund_After2PM_ReturnsZero()
    {
        var checkOut = DateTime.Today.AddHours(14); // exactly 2:00 PM
        _sut.CalculateRefund(checkOut, 20m).Should().Be(0m);
    }

    [Fact]
    public void CalculateRefund_Before2PM_ReturnsDeposit()
    {
        var checkOut = DateTime.Today.AddHours(13).AddMinutes(59); // 1:59 PM
        _sut.CalculateRefund(checkOut, 20m).Should().Be(20m);
    }

    // BR-21: Temporary receipt — SubTotal = Payment - Deposit
    [Fact]
    public void TemporaryReceiptSubTotal_IsPaymentMinusDeposit()
    {
        _sut.TemporaryReceiptSubTotal(320m, 20m).Should().Be(300m);
    }

    // BR-22: Official receipt — Total = Payment - Refund
    [Fact]
    public void OfficialReceiptTotal_IsPaymentMinusRefund()
    {
        _sut.OfficialReceiptTotal(320m, 20m).Should().Be(300m);
    }
}

/// <summary>
/// BDD tests for Room entity state machine — BR-11
/// </summary>
public class RoomStateMachineTests
{
    [Theory]
    [InlineData(RoomStatus.Open, RoomStatus.Booked, true)]
    [InlineData(RoomStatus.Booked, RoomStatus.Occupied, true)]
    [InlineData(RoomStatus.Occupied, RoomStatus.Housekeeping, true)]
    [InlineData(RoomStatus.Housekeeping, RoomStatus.Open, true)]
    [InlineData(RoomStatus.Open, RoomStatus.Occupied, false)]     // invalid skip
    [InlineData(RoomStatus.Open, RoomStatus.Housekeeping, false)] // invalid skip
    [InlineData(RoomStatus.Booked, RoomStatus.Open, false)]       // cannot go back
    [InlineData(RoomStatus.Occupied, RoomStatus.Booked, false)]   // cannot go back
    public void CanTransitionTo_ValidAndInvalidTransitions(
        RoomStatus from, RoomStatus to, bool expected)
    {
        var room = new Room { RoomStatus = from };
        room.CanTransitionTo(to).Should().Be(expected);
    }

    [Fact]
    public void AnyStatus_CanTransitionToMaintenance()
    {
        foreach (var status in Enum.GetValues<RoomStatus>())
        {
            var room = new Room { RoomStatus = status };
            room.CanTransitionTo(RoomStatus.Maintenance).Should().BeTrue(
                $"{status} should be able to transition to Maintenance");
        }
    }

    // BR-16
    [Fact]
    public void CanBeBooked_ReturnsFalse_WhenRoomInMaintenance()
    {
        var room = new Room { RoomStatus = RoomStatus.Maintenance, Active = true };
        room.CanBeBooked().Should().BeFalse();
    }

    [Fact]
    public void CanBeBooked_ReturnsTrue_WhenRoomIsOpen()
    {
        var room = new Room { RoomStatus = RoomStatus.Open, Active = true };
        room.CanBeBooked().Should().BeTrue();
    }
}