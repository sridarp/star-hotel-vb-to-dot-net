using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StarHotel.Api.Middleware;
using StarHotel.Api.Services;
using StarHotel.Domain.Interfaces;
using StarHotel.Infrastructure.Messaging;
using StarHotel.Infrastructure.Persistence;
using StarHotel.Infrastructure.RealTime;
using StarHotel.Infrastructure.Repositories;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Application Insights ──────────────────────────────────────────────────────
builder.Services.AddApplicationInsightsTelemetry();

// ── Database — EF Core + Azure SQL ───────────────────────────────────────────
builder.Services.AddDbContext<StarHotelDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        }));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();

// ── Business Services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<DocumentService>();

// ── Messaging — Azure Service Bus ────────────────────────────────────────────
builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

// ── Real-Time — SignalR ───────────────────────────────────────────────────────
var signalRConnStr = builder.Configuration["AzureSignalR:ConnectionString"];
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(signalRConnStr))
{
    signalRBuilder.AddAzureSignalR(signalRConnStr);
}
builder.Services.AddScoped<IDashboardNotifier, DashboardNotifier>();

// ── Redis Cache ───────────────────────────────────────────────────────────────
var redisConnStr = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnStr))
{
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnStr);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// ── Authentication — JWT Bearer ───────────────────────────────────────────────
// Supports both Entra ID (production) and local JWT (development/testing)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // SignalR websocket token support
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// ── Authorization Policies (maps to ModuleAccess in DB) ───────────────────────
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("DashboardRead",     p => p.RequireAuthenticatedUser());
    opts.AddPolicy("BookingRead",       p => p.RequireAuthenticatedUser());
    opts.AddPolicy("BookingWrite",      p => p.RequireRole("Administrator", "Clerk"));
    opts.AddPolicy("ReportList",        p => p.RequireAuthenticatedUser());
    opts.AddPolicy("FindCustomer",      p => p.RequireAuthenticatedUser());
    opts.AddPolicy("RoomMaintain",      p => p.RequireRole("Administrator", "Manager"));
    opts.AddPolicy("UserMaintain",      p => p.RequireRole("Administrator"));
    opts.AddPolicy("AccessControl",     p => p.RequireRole("Administrator"));
});

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Star Hotel API",
        Version = "v1",
        Description = "Hotel Room Reservation System — modernized from VB6"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddPolicy("SpaPolicy", policy =>
        policy.WithOrigins(
                "http://localhost:5173",  // Vite dev server
                "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<StarHotelDbContext>("database")
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
        name: "sqlserver");

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Star Hotel API v1"));
}

app.UseHttpsRedirection();
app.UseCors("SpaPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hub
app.MapHub<DashboardHub>("/hubs/dashboard");

// Health endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true });

// ── Auto-migrate database on startup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarHotelDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migration completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed");
        // In production, fail fast — don't start with broken DB
        if (!app.Environment.IsDevelopment()) throw;
    }
}

await app.RunAsync();

// Expose Program for integration tests
public partial class Program { }