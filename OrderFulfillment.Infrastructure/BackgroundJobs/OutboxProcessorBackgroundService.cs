using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFulfillment.Application.Common.Events;
using OrderFulfillment.Domain.Common;
using OrderFulfillment.Infrastructure.Data;
using OrderFulfillment.Infrastructure.Outbox;

namespace OrderFulfillment.Infrastructure.BackgroundJobs;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private static readonly Dictionary<string, Type> DomainEventTypes =
        typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where((Type t) => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToDictionary((Type t) => t.Name, (Type t) => t);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly TimeSpan _period;
    private const int BatchSize = 20;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger,
        TimeSpan? period = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _period = period ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor Background Service started with polling interval {IntervalSeconds}s.", _period.TotalSeconds);

        using PeriodicTimer timer = new PeriodicTimer(_period);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "An error occurred while executing outbox messages processing cycle.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox Processor Background Service is shutting down gracefully.");
    }

    public async Task<int> ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPublisher publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        List<OutboxMessage> messages = await GetUnprocessedMessagesAsync(dbContext, cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug("Fetched {Count} unprocessed outbox messages.", messages.Count);

        int processedCount = 0;

        foreach (OutboxMessage message in messages)
        {
            bool success = await ProcessSingleMessageAsync(message, publisher, cancellationToken);
            if (success)
            {
                processedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return processedCount;
    }

    private static async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.OutboxMessages
            .Where((OutboxMessage m) => m.ProcessedOnUtc == null)
            .OrderBy((OutboxMessage m) => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> ProcessSingleMessageAsync(
        OutboxMessage message,
        IPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (!TryDeserializeDomainEvent(message, out IDomainEvent? domainEvent, out Type? eventType))
        {
            return false;
        }

        try
        {
            await PublishDomainEventAsync(publisher, domainEvent!, eventType!, cancellationToken);

            message.ProcessedOnUtc = DateTime.UtcNow;
            message.Error = null;

            _logger.LogInformation("Successfully dispatched domain event '{EventType}' from outbox message {MessageId}.", message.Type, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch domain event '{EventType}' from outbox message {MessageId}.", message.Type, message.Id);
            message.Error = ex.ToString();
            return false;
        }
    }

    private bool TryDeserializeDomainEvent(
        OutboxMessage message,
        out IDomainEvent? domainEvent,
        out Type? eventType)
    {
        domainEvent = null;

        if (!DomainEventTypes.TryGetValue(message.Type, out eventType))
        {
            _logger.LogWarning("Unknown domain event type '{EventType}' for outbox message {MessageId}.", message.Type, message.Id);
            message.Error = $"Unknown domain event type '{message.Type}'.";
            return false;
        }

        try
        {
            domainEvent = JsonSerializer.Deserialize(message.Content, eventType) as IDomainEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize outbox message {MessageId} into type '{EventType}'.", message.Id, message.Type);
            message.Error = $"Deserialization error: {ex.Message}";
            return false;
        }

        if (domainEvent is null)
        {
            _logger.LogError("Deserialization of outbox message {MessageId} returned null.", message.Id);
            message.Error = "Deserialization resulted in null.";
            return false;
        }

        return true;
    }

    private static async Task PublishDomainEventAsync(
        IPublisher publisher,
        IDomainEvent domainEvent,
        Type eventType,
        CancellationToken cancellationToken)
    {
        Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        object notification = Activator.CreateInstance(notificationType, domainEvent)!;

        await publisher.Publish(notification, cancellationToken);
    }
}
