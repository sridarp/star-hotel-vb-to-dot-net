using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace StarHotel.Infrastructure.RealTime;

/// <summary>
/// SignalR Hub for real-time room status push (replaces tmrClock_Timer polling — BR-25)
/// </summary>
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join hotel-specific group for targeted broadcasts
    /// </summary>
    public async Task JoinDashboard(string hotelId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hotel-{hotelId}");
}

/// <summary>
/// Service to push room-status updates to all connected dashboard clients (BR-25 blink events)
/// </summary>
public interface IDashboardNotifier
{
    Task NotifyRoomStatusChangedAsync(int roomId, string status, bool shouldBlink, CancellationToken ct = default);
    Task NotifyRoomSummaryChangedAsync(object summary, CancellationToken ct = default);
}

public class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hub;

    public DashboardNotifier(IHubContext<DashboardHub> hub) => _hub = hub;

    public async Task NotifyRoomStatusChangedAsync(int roomId, string status, bool shouldBlink, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("RoomStatusChanged", new
        {
            roomId,
            status,
            shouldBlink,
            timestamp = DateTime.UtcNow
        }, ct);
    }

    public async Task NotifyRoomSummaryChangedAsync(object summary, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("SummaryChanged", summary, ct);
    }
}