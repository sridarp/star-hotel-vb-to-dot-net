using StarHotel.Domain.Enums;

namespace StarHotel.Domain.Entities;

/// <summary>
/// Booking entity — core transactional domain (BR-12–BR-24)
/// </summary>
public class Booking
{
    public long Id { get; set; }

    // Guest details
    public string GuestName { get; set; } = string.Empty;
    public string GuestPassport { get; set; } = string.Empty;
    public string GuestOrigin { get; set; } = string.Empty;
    public string GuestContact { get; set; } = string.Empty;
    public string GuestEmergencyContactName { get; set; } = string.Empty;
    public string GuestEmergencyContactNo { get; set; } = string.Empty;

    // Booking details
    public int TotalGuest { get; set; }
    public int StayDuration { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime GuestCheckIn { get; set; }
    public DateTime GuestCheckOut { get; set; }
    public string Remarks { get; set; } = string.Empty;

    // Room snapshot (denormalized for reporting)
    public int RoomId { get; set; }
    public string RoomNo { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string RoomLocation { get; set; } = string.Empty;
    public decimal RoomPrice { get; set; }
    public bool Breakfast { get; set; }
    public decimal BreakfastPrice { get; set; }

    // Financial (BR-18–BR-22)
    public decimal SubTotal { get; set; }       // StayDuration × RoomPrice
    public decimal Deposit { get; set; } = 20m; // BR-20: default 20.00
    public decimal Payment { get; set; }
    public decimal Refund { get; set; }

    // Lifecycle
    public bool Active { get; set; } = true;
    public bool Temp { get; set; } = true;      // BR-24: temp record pattern

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastModifiedDate { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public Room? Room { get; set; }

    /// <summary>
    /// BR-13 / BR-14: Payment must equal SubTotal + Deposit to allow check-in/out
    /// </summary>
    public bool IsPaid() => Payment == SubTotal + Deposit;

    /// <summary>
    /// BR-21: Temporary receipt — SubTotal = Payment - Deposit
    /// </summary>
    public decimal TemporaryReceiptSubTotal => Payment - Deposit;

    /// <summary>
    /// BR-22: Official receipt — Total = Payment - Refund
    /// </summary>
    public decimal OfficialReceiptTotal => Payment - Refund;

    /// <summary>
    /// BR-24: 6-digit formatted booking ID
    /// </summary>
    public string FormattedId => Id.ToString("D6");

    /// <summary>
    /// BR-19: TotalDue = SubTotal + Deposit
    /// </summary>
    public decimal TotalDue => SubTotal + Deposit;
}

/// <summary>
/// Room entity — owns lifecycle state machine (BR-11)
/// </summary>
public class Room
{
    public int Id { get; set; }
    public long BookingId { get; set; }
    public string RoomShortName { get; set; } = string.Empty;
    public string RoomLongName { get; set; } = string.Empty;
    public RoomStatus RoomStatus { get; set; } = RoomStatus.Open;
    public string RoomType { get; set; } = string.Empty;
    public string RoomLocation { get; set; } = string.Empty;
    public decimal RoomPrice { get; set; }
    public bool Breakfast { get; set; }
    public decimal BreakfastPrice { get; set; }
    public bool Maintenance { get; set; }
    public bool Active { get; set; } = true;

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public DateTime? LastModifiedDate { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public Booking? CurrentBooking { get; set; }

    /// <summary>
    /// BR-16: Maintenance rooms cannot be booked
    /// </summary>
    public bool CanBeBooked() => RoomStatus != RoomStatus.Maintenance && Active;

    /// <summary>
    /// Valid status transitions per BR-11
    /// </summary>
    public bool CanTransitionTo(RoomStatus target) => (RoomStatus, target) switch
    {
        (RoomStatus.Open, RoomStatus.Booked) => true,
        (RoomStatus.Booked, RoomStatus.Occupied) => true,
        (RoomStatus.Occupied, RoomStatus.Housekeeping) => true,
        (RoomStatus.Housekeeping, RoomStatus.Open) => true,
        (_, RoomStatus.Maintenance) => true,
        (RoomStatus.Maintenance, RoomStatus.Open) => true,
        _ => false
    };
}

/// <summary>
/// Room type catalogue (BR-18: RoomPrice source)
/// </summary>
public class RoomType
{
    public int Id { get; set; }
    public string TypeShortName { get; set; } = string.Empty;
    public string TypeLongName { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}

/// <summary>
/// User data entity — replaces VB6 UserData table
/// Passwords managed by Entra ID; legacy fields kept for migration
/// </summary>
public class UserData
{
    public int Id { get; set; }
    public int UserGroup { get; set; }
    public string UserId { get; set; } = string.Empty;  // BR-01: uppercase
    public string UserName { get; set; } = string.Empty;
    public int Idle { get; set; }                        // BR-09/10: session timeout
    public int LoginAttempts { get; set; }               // BR-02: lockout counter
    public bool ChangePassword { get; set; }             // BR-07: force change
    public bool DashboardBlink { get; set; } = true;     // BR-25: blink preference
    public bool Active { get; set; } = true;             // BR-03: frozen account

    // Navigation
    public UserGroup? Group { get; set; }
}

/// <summary>
/// User group — maps to Entra ID app roles
/// </summary>
public class UserGroup
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupDesc { get; set; } = string.Empty;
    public long SecurityLevel { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>
/// Module access control — maps to Entra ID role-permission matrix (BR-05)
/// </summary>
public class ModuleAccess
{
    public int ModuleId { get; set; }
    public string ModuleDesc { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
    public bool Group1 { get; set; }  // Administrator
    public bool Group2 { get; set; }  // Manager
    public bool Group3 { get; set; }  // Supervisor
    public bool Group4 { get; set; }  // Clerk
    public bool Active { get; set; } = true;
}

/// <summary>
/// Company / hotel configuration
/// </summary>
public class Company
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string ContactNo { get; set; } = string.Empty;
    public DateTime SystemStartDate { get; set; }
    public string ProductVersion { get; set; } = "2.0";
    public double DatabaseVersion { get; set; } = 2.0;
    public string CurrencySymbol { get; set; } = "MYR";
    public bool Active { get; set; } = true;
}

/// <summary>
/// Booking audit log
/// </summary>
public class LogBooking
{
    public long Id { get; set; }
    public long BookingId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string RoomNo { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal Payment { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Application error log — replaces LogErrorText/LogErrorDB (dual-channel)
/// </summary>
public class LogError
{
    public long Id { get; set; }
    public DateTime LogDateTime { get; set; } = DateTime.UtcNow;
    public string LogErrorNum { get; set; } = string.Empty;
    public string LogErrorDescription { get; set; } = string.Empty;
    public string LogUserName { get; set; } = string.Empty;
    public string LogModule { get; set; } = string.Empty;
    public string LogMethod { get; set; } = string.Empty;
    public string LogType { get; set; } = string.Empty;
}

/// <summary>
/// Weekly booking aggregate for chart reports (BR-27)
/// </summary>
public class WeeklyBooking
{
    public int Id { get; set; }
    public decimal RoomPrice { get; set; }
    public decimal BreakfastPrice { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Deposit { get; set; }
    public decimal Payment { get; set; }
    public decimal Refund { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}