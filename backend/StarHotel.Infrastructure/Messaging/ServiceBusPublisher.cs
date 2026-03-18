using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StarHotel.Domain.Events;
using System.Text.Json;

namespace StarHotel.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string topicOrQueue, CancellationToken ct = default) where T : class;
}

public class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient? _client;
    private readonly ILogger<ServiceBusEventPublisher> _logger;
    private readonly bool _enabled;

    public ServiceBusEventPublisher(IConfiguration config, ILogger<ServiceBusEventPublisher> logger)
    {
        _logger = logger;
        var connStr = config.GetConnectionString("ServiceBus");
        if (!string.IsNullOrEmpty(connStr))
        {
            _client = new ServiceBusClient(connStr);
            _enabled = true;
        }
        else
        {
            _logger.LogWarning("ServiceBus connection string not configured — events will be logged only");
            _enabled = false;
        }
    }

    public async Task PublishAsync<T>(T @event, string topicOrQueue, CancellationToken ct = default) where T : class
    {
        var payload = JsonSerializer.Serialize(@event, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _logger.LogInformation("Publishing event {EventType} to {Destination}: {Payload}",
            typeof(T).Name, topicOrQueue, payload);

        if (!_enabled || _client == null) return;

        try
        {
            await using var sender = _client.CreateSender(topicOrQueue);
            var message = new ServiceBusMessage(payload)
            {
                ContentType = "application/json",
                Subject = typeof(T).Name
            };
            await sender.SendMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to {Destination}", typeof(T).Name, topicOrQueue);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null) await _client.DisposeAsync();
    }
}

/// <summary>
/// Queue/topic names for Azure Service Bus
/// </summary>
public static class ServiceBusQueues
{
    public const string BookingEvents = "booking-events";
    public const string DocumentEvents = "document-events";
    public const string ReportEvents = "report-events";
}