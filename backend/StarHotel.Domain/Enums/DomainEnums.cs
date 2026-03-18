namespace StarHotel.Domain.Enums;

/// <summary>
/// Room status lifecycle: Open → Booked → Occupied → Housekeeping → Open
/// Maintenance and Void are orthogonal states (BR-11)
/// </summary>
public enum RoomStatus
{
    Open,
    Booked,
    Occupied,
    Housekeeping,
    Maintenance,
    Void
}

/// <summary>
/// User group levels matching legacy VB6 UserGroup (1-4)
/// </summary>
public enum UserGroupLevel
{
    Administrator = 1,
    Manager = 2,
    Supervisor = 3,
    Clerk = 4
}

/// <summary>
/// Module access IDs matching legacy modGlobal.bas MOD_* constants
/// </summary>
public enum ModuleId
{
    Dashboard = 1,
    Booking = 2,
    ReportList = 3,
    ReportPrint = 4,
    ReportExport = 5,
    ReportEdit = 6,
    ReportEditExpert = 7,
    FindCustomer = 8,
    MaintainRoom = 9,
    MaintainUser = 10,
    AccessControl = 11,
    // Reports (12-18)
    DailyBooking = 12,
    WeeklyBooking = 13,
    MonthlyBooking = 14,
    WeeklyBookingGraph = 15,
    ShiftReportForUser = 16,
    ShiftReportAllUsers = 17,
    OfficialReceiptReprint = 18
}

public enum ReceiptType
{
    Temporary,  // BR-21: SubTotal = Payment - Deposit
    Official    // BR-22: Total = Payment - Refund
}