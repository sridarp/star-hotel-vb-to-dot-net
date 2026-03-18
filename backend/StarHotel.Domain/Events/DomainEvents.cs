namespace StarHotel.Domain.Events;

/// <summary>
/// Domain events published to Azure Service Bus
/// </summary>

public record BookingCreatedEvent(
    long BookingId,
    string GuestName,
    int RoomId,
    string RoomNo,
    decimal Payment,
    string CreatedBy,
    DateTime Timestamp);

public record CheckInEvent(
    long BookingId,
    int RoomId,
    string RoomNo,
    DateTime CheckInTime,
    string ProcessedBy,
    DateTime Timestamp);

public record CheckOutEvent(
    long BookingId,
    int RoomId,
    string RoomNo,
    DateTime CheckOutTime,
    decimal Refund,
    string ProcessedBy,
    DateTime Timestamp);

public record BookingVoidedEvent(
    long BookingId,
    bool IsVoided,
    string ProcessedBy,
    DateTime Timestamp);