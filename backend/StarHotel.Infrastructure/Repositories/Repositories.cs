using Microsoft.EntityFrameworkCore;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.Persistence;

namespace StarHotel.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly StarHotelDbContext _ctx;

    public BookingRepository(StarHotelDbContext ctx) => _ctx = ctx;

    public async Task<Booking?> GetByIdAsync(long id, CancellationToken ct = default) =>
        await _ctx.Bookings.Include(b => b.Room).FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<List<Booking>> GetActiveBookingsAsync(CancellationToken ct = default) =>
        await _ctx.Bookings
            .Where(b => b.Active && !b.Temp)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync(ct);

    /// <summary>
    /// BR-24: Create a temp booking record to reserve an ID before the form is filled
    /// </summary>
    public async Task<long> CreateTempBookingAsync(string createdBy, CancellationToken ct = default)
    {
        // Check if a temp booking already exists for this user (reuse pattern from VB6)
        var existing = await _ctx.Bookings
            .Where(b => b.Temp && b.CreatedBy == createdBy)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != null) return existing.Id;

        var temp = new Booking
        {
            Temp = true,
            Active = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        _ctx.Bookings.Add(temp);
        await _ctx.SaveChangesAsync(ct);
        return temp.Id;
    }

    public async Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        _ctx.Bookings.Update(booking);
        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// BR-13 / BR-14: Payment must equal SubTotal + Deposit
    /// </summary>
    public async Task<bool> IsPaidAsync(long bookingId, CancellationToken ct = default)
    {
        var b = await _ctx.Bookings.FindAsync([bookingId], ct);
        return b != null && b.Payment == b.SubTotal + b.Deposit;
    }
}

public class RoomRepository : IRoomRepository
{
    private readonly StarHotelDbContext _ctx;

    public RoomRepository(StarHotelDbContext ctx) => _ctx = ctx;

    public async Task<Room?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _ctx.Rooms.FindAsync([id], ct);

    public async Task<List<Room>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _ctx.Rooms.Where(r => r.Active).OrderBy(r => r.Id).ToListAsync(ct);

    public async Task<List<Room>> GetAllAsync(CancellationToken ct = default) =>
        await _ctx.Rooms.OrderBy(r => r.Id).ToListAsync(ct);

    public async Task<string> GetRoomStatusAsync(int roomId, CancellationToken ct = default)
    {
        var room = await _ctx.Rooms.FindAsync([roomId], ct);
        return room?.RoomStatus.ToString() ?? string.Empty;
    }

    public async Task UpdateStatusAsync(int roomId, RoomStatus status, long bookingId, string modifiedBy, CancellationToken ct = default)
    {
        var room = await _ctx.Rooms.FindAsync([roomId], ct)
            ?? throw new InvalidOperationException($"Room {roomId} not found");

        room.RoomStatus = status;
        if (status == RoomStatus.Booked) room.BookingId = bookingId;
        room.LastModifiedDate = DateTime.UtcNow;
        room.LastModifiedBy = modifiedBy;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> RoomExistsAsync(int roomId, CancellationToken ct = default) =>
        await _ctx.Rooms.AnyAsync(r => r.Id == roomId, ct);

    public async Task<RoomSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var counts = await _ctx.Rooms
            .Where(r => r.Active)
            .GroupBy(r => r.RoomStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new RoomSummary(
            Open: counts.FirstOrDefault(c => c.Status == RoomStatus.Open)?.Count ?? 0,
            Booked: counts.FirstOrDefault(c => c.Status == RoomStatus.Booked)?.Count ?? 0,
            Occupied: counts.FirstOrDefault(c => c.Status == RoomStatus.Occupied)?.Count ?? 0,
            Housekeeping: counts.FirstOrDefault(c => c.Status == RoomStatus.Housekeeping)?.Count ?? 0,
            Maintenance: counts.FirstOrDefault(c => c.Status == RoomStatus.Maintenance)?.Count ?? 0
        );
    }

    public async Task AddAsync(Room room, CancellationToken ct = default)
    {
        _ctx.Rooms.Add(room);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<List<RoomType>> GetRoomTypesAsync(CancellationToken ct = default) =>
        await _ctx.RoomTypes.Where(rt => rt.Active).ToListAsync(ct);
}

public class UserRepository : IUserRepository
{
    private readonly StarHotelDbContext _ctx;

    public UserRepository(StarHotelDbContext ctx) => _ctx = ctx;

    public async Task<UserData?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await _ctx.Users
            .FirstOrDefaultAsync(u => u.UserId == userId.ToUpper(), ct); // BR-01: uppercase

    public async Task<bool> HasModuleAccessAsync(string userId, int moduleId, CancellationToken ct = default)
    {
        var user = await GetByUserIdAsync(userId, ct);
        if (user == null) return false;

        var module = await _ctx.ModuleAccesses.FindAsync([moduleId], ct);
        if (module == null) return false;

        return user.UserGroup switch
        {
            1 => module.Group1,
            2 => module.Group2,
            3 => module.Group3,
            4 => module.Group4,
            _ => false
        };
    }

    public async Task UpdateAsync(UserData user, CancellationToken ct = default)
    {
        _ctx.Users.Update(user);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<List<UserData>> GetAllAsync(CancellationToken ct = default) =>
        await _ctx.Users.OrderBy(u => u.UserId).ToListAsync(ct);

    public async Task AddAsync(UserData user, CancellationToken ct = default)
    {
        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync(ct);
    }
}

public class CompanyRepository : ICompanyRepository
{
    private readonly StarHotelDbContext _ctx;

    public CompanyRepository(StarHotelDbContext ctx) => _ctx = ctx;

    public async Task<Company?> GetAsync(CancellationToken ct = default) =>
        await _ctx.Companies.FirstOrDefaultAsync(ct);

    public async Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _ctx.Companies.Update(company);
        await _ctx.SaveChangesAsync(ct);
    }
}