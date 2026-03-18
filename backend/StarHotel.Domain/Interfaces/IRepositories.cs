using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;

namespace StarHotel.Domain.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<Booking>> GetActiveBookingsAsync(CancellationToken ct = default);
    Task<long> CreateTempBookingAsync(string createdBy, CancellationToken ct = default);
    Task UpdateAsync(Booking booking, CancellationToken ct = default);
    Task<bool> IsPaidAsync(long bookingId, CancellationToken ct = default);
}

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Room>> GetAllActiveAsync(CancellationToken ct = default);
    Task<string> GetRoomStatusAsync(int roomId, CancellationToken ct = default);
    Task UpdateStatusAsync(int roomId, RoomStatus status, long bookingId, string modifiedBy, CancellationToken ct = default);
    Task<bool> RoomExistsAsync(int roomId, CancellationToken ct = default);
    Task<RoomSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<List<Room>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task<List<RoomType>> GetRoomTypesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<UserData?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<bool> HasModuleAccessAsync(string userId, int moduleId, CancellationToken ct = default);
    Task UpdateAsync(UserData user, CancellationToken ct = default);
    Task<List<UserData>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(UserData user, CancellationToken ct = default);
}

public interface ICompanyRepository
{
    Task<Company?> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(Company company, CancellationToken ct = default);
}

/// <summary>
/// Room summary counts for dashboard display
/// </summary>
public record RoomSummary(
    int Open,
    int Booked,
    int Occupied,
    int Housekeeping,
    int Maintenance);