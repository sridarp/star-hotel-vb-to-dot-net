using Microsoft.EntityFrameworkCore;
using StarHotel.Domain.Entities;
using StarHotel.Domain.Enums;

namespace StarHotel.Infrastructure.Persistence;

public class StarHotelDbContext : DbContext
{
    public StarHotelDbContext(DbContextOptions<StarHotelDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<UserData> Users => Set<UserData>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<ModuleAccess> ModuleAccesses => Set<ModuleAccess>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<LogBooking> LogBookings => Set<LogBooking>();
    public DbSet<LogError> LogErrors => Set<LogError>();
    public DbSet<WeeklyBooking> WeeklyBookings => Set<WeeklyBooking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Booking
        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Booking");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).ValueGeneratedOnAdd();
            e.Property(b => b.GuestName).HasMaxLength(50);
            e.Property(b => b.GuestPassport).HasMaxLength(50);
            e.Property(b => b.GuestOrigin).HasMaxLength(50);
            e.Property(b => b.GuestContact).HasMaxLength(50);
            e.Property(b => b.GuestEmergencyContactName).HasMaxLength(50);
            e.Property(b => b.GuestEmergencyContactNo).HasMaxLength(50);
            e.Property(b => b.RoomNo).HasMaxLength(50);
            e.Property(b => b.RoomType).HasMaxLength(50);
            e.Property(b => b.RoomLocation).HasMaxLength(50);
            e.Property(b => b.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(b => b.Deposit).HasColumnType("decimal(18,2)").HasDefaultValue(20m);
            e.Property(b => b.Payment).HasColumnType("decimal(18,2)");
            e.Property(b => b.Refund).HasColumnType("decimal(18,2)");
            e.Property(b => b.RoomPrice).HasColumnType("decimal(18,2)");
            e.Property(b => b.BreakfastPrice).HasColumnType("decimal(18,2)");
            e.Property(b => b.CreatedBy).HasMaxLength(50);
            e.Property(b => b.LastModifiedBy).HasMaxLength(50);
            e.HasIndex(b => new { b.RoomId, b.Temp, b.Active });
        });

        // Room
        modelBuilder.Entity<Room>(e =>
        {
            e.ToTable("Room");
            e.HasKey(r => r.Id);
            e.Property(r => r.RoomShortName).HasMaxLength(50);
            e.Property(r => r.RoomLongName).HasMaxLength(255);
            e.Property(r => r.RoomType).HasMaxLength(50);
            e.Property(r => r.RoomLocation).HasMaxLength(50);
            e.Property(r => r.RoomPrice).HasColumnType("decimal(18,2)");
            e.Property(r => r.BreakfastPrice).HasColumnType("decimal(18,2)");
            e.Property(r => r.CreatedBy).HasMaxLength(50).HasDefaultValue("System");
            e.Property(r => r.LastModifiedBy).HasMaxLength(50);
            e.Property(r => r.RoomStatus)
                .HasConversion<string>()
                .HasMaxLength(20);
            e.HasIndex(r => r.RoomStatus);
        });

        // RoomType
        modelBuilder.Entity<RoomType>(e =>
        {
            e.ToTable("RoomType");
            e.HasKey(r => r.Id);
            e.Property(r => r.TypeShortName).HasMaxLength(30);
            e.Property(r => r.TypeLongName).HasMaxLength(255);
        });

        // UserData
        modelBuilder.Entity<UserData>(e =>
        {
            e.ToTable("UserData");
            e.HasKey(u => u.Id);
            e.Property(u => u.UserId).HasMaxLength(20);
            e.Property(u => u.UserName).HasMaxLength(50);
            e.HasIndex(u => u.UserId).IsUnique();
        });

        // UserGroup
        modelBuilder.Entity<UserGroup>(e =>
        {
            e.ToTable("UserGroup");
            e.HasKey(g => g.GroupId);
            e.Property(g => g.GroupName).HasMaxLength(20);
            e.Property(g => g.GroupDesc).HasMaxLength(255);
        });

        // ModuleAccess
        modelBuilder.Entity<ModuleAccess>(e =>
        {
            e.ToTable("ModuleAccess");
            e.HasKey(m => m.ModuleId);
            e.Property(m => m.ModuleDesc).HasMaxLength(50);
            e.Property(m => m.ModuleType).HasMaxLength(50);
        });

        // Company
        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("Company");
            e.HasKey(c => c.Id);
            e.Property(c => c.CompanyName).HasMaxLength(255);
            e.Property(c => c.StreetAddress).HasMaxLength(255);
            e.Property(c => c.ContactNo).HasMaxLength(50);
            e.Property(c => c.ProductVersion).HasMaxLength(50);
            e.Property(c => c.CurrencySymbol).HasMaxLength(3);
        });

        // LogBooking
        modelBuilder.Entity<LogBooking>(e =>
        {
            e.ToTable("LogBooking");
            e.HasKey(l => l.Id);
            e.Property(l => l.GuestName).HasMaxLength(50);
            e.Property(l => l.RoomNo).HasMaxLength(50);
            e.Property(l => l.RoomType).HasMaxLength(50);
            e.Property(l => l.CreatedBy).HasMaxLength(50);
        });

        // LogError
        modelBuilder.Entity<LogError>(e =>
        {
            e.ToTable("LogError");
            e.HasKey(l => l.Id);
            e.Property(l => l.LogErrorNum).HasMaxLength(50);
            e.Property(l => l.LogUserName).HasMaxLength(50);
            e.Property(l => l.LogModule).HasMaxLength(255);
            e.Property(l => l.LogMethod).HasMaxLength(255);
            e.Property(l => l.LogType).HasMaxLength(50);
        });

        // WeeklyBooking
        modelBuilder.Entity<WeeklyBooking>(e =>
        {
            e.ToTable("WeeklyBooking");
            e.HasKey(w => w.Id);
            e.Property(w => w.RoomPrice).HasColumnType("decimal(18,2)");
            e.Property(w => w.BreakfastPrice).HasColumnType("decimal(18,2)");
            e.Property(w => w.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(w => w.Deposit).HasColumnType("decimal(18,2)");
            e.Property(w => w.Payment).HasColumnType("decimal(18,2)");
            e.Property(w => w.Refund).HasColumnType("decimal(18,2)");
            e.Property(w => w.CreatedBy).HasMaxLength(50);
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Company
        modelBuilder.Entity<Company>().HasData(new Company
        {
            Id = 1,
            CompanyName = "STAR HOTEL",
            StreetAddress = "9, Jalan Bintang, 50100 Kuala Lumpur, Malaysia",
            ContactNo = "Tel/Fax : +603 - 4200 6336",
            SystemStartDate = new DateTime(2018, 1, 1),
            ProductVersion = "2.0",
            DatabaseVersion = 2.0,
            CurrencySymbol = "MYR",
            Active = true
        });

        // User Groups
        modelBuilder.Entity<UserGroup>().HasData(
            new UserGroup { GroupId = 1, GroupName = "Administrator", GroupDesc = "Highest Level User Group", SecurityLevel = 99, Active = true },
            new UserGroup { GroupId = 2, GroupName = "Manager", GroupDesc = "Cannot access Admin level", SecurityLevel = 98, Active = false },
            new UserGroup { GroupId = 3, GroupName = "Supervisor", GroupDesc = "Supervisor", SecurityLevel = 20, Active = false },
            new UserGroup { GroupId = 4, GroupName = "Clerk", GroupDesc = "Cashier", SecurityLevel = 10, Active = true }
        );

        // Module Access (11 modules + 7 reports = 18 entries)
        var modules = new[]
        {
            new ModuleAccess { ModuleId = 1,  ModuleDesc = "Dashboard",               ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = true,  Active = true },
            new ModuleAccess { ModuleId = 2,  ModuleDesc = "Booking",                 ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = true,  Active = true },
            new ModuleAccess { ModuleId = 3,  ModuleDesc = "List Report",             ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = true,  Active = true },
            new ModuleAccess { ModuleId = 4,  ModuleDesc = "Print Report",            ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 5,  ModuleDesc = "Export Report",           ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 6,  ModuleDesc = "Edit Report",             ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 7,  ModuleDesc = "Edit Report (Expert)",    ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 8,  ModuleDesc = "Find Customer",           ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = true,  Active = true },
            new ModuleAccess { ModuleId = 9,  ModuleDesc = "Maintain Room",           ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 10, ModuleDesc = "Maintain User",           ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 11, ModuleDesc = "Access Control",          ModuleType = "Form",   Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 12, ModuleDesc = "Daily Booking Report",    ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 13, ModuleDesc = "Weekly Booking Report",   ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 14, ModuleDesc = "Monthly Booking Report",  ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 15, ModuleDesc = "Weekly Booking Graph",    ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 16, ModuleDesc = "Shift Report for User",   ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = true,  Active = true },
            new ModuleAccess { ModuleId = 17, ModuleDesc = "Shift Report (All Users)",ModuleType = "Report", Group1 = true,  Group2 = false, Group3 = false, Group4 = false, Active = true },
            new ModuleAccess { ModuleId = 18, ModuleDesc = "Official Receipt (Reprint)", ModuleType = "Report", Group1 = true, Group2 = false, Group3 = false, Group4 = false, Active = true }
        };
        modelBuilder.Entity<ModuleAccess>().HasData(modules);

        // Room Types
        modelBuilder.Entity<RoomType>().HasData(
            new RoomType { Id = 1, TypeShortName = "SINGLE BED ROOM", TypeLongName = "", Active = true },
            new RoomType { Id = 2, TypeShortName = "DOUBLE BED ROOM", TypeLongName = "", Active = true },
            new RoomType { Id = 3, TypeShortName = "TWIN BED ROOM",   TypeLongName = "", Active = true },
            new RoomType { Id = 4, TypeShortName = "DORM",            TypeLongName = "", Active = true }
        );

        // Sample Room (Room 101)
        modelBuilder.Entity<Room>().HasData(new Room
        {
            Id = 1,
            BookingId = 0,
            RoomShortName = "101",
            RoomLongName = "",
            RoomStatus = RoomStatus.Open,
            RoomType = "SINGLE BED ROOM",
            RoomLocation = "Level 1",
            RoomPrice = 100m,
            Breakfast = true,
            BreakfastPrice = 10m,
            Maintenance = false,
            Active = true,
            CreatedBy = "System"
        });

        // Sample Users (Admin and Clerk)
        modelBuilder.Entity<UserData>().HasData(
            new UserData
            {
                Id = 1,
                UserGroup = 1,
                UserId = "ADMIN",
                UserName = "Demo Administrator",
                Idle = 0,
                LoginAttempts = 0,
                ChangePassword = false,
                DashboardBlink = true,
                Active = true
            },
            new UserData
            {
                Id = 2,
                UserGroup = 4,
                UserId = "CLERK",
                UserName = "Receptionist",
                Idle = 300,
                LoginAttempts = 0,
                ChangePassword = false,
                DashboardBlink = true,
                Active = true
            }
        );
    }
}