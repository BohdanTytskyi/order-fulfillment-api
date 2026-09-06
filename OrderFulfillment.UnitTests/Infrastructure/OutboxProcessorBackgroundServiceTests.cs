using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrderFulfillment.Application.Common.Events;
using OrderFulfillment.Domain.Events;
using OrderFulfillment.Domain.ValueObjects;
using OrderFulfillment.Infrastructure.BackgroundJobs;
using OrderFulfillment.Infrastructure.Data;
using OrderFulfillment.Infrastructure.Outbox;
using Xunit;

namespace OrderFulfillment.UnitTests.Infrastructure;

public class OutboxProcessorBackgroundServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly OutboxProcessorBackgroundService _processor;

    public OutboxProcessorBackgroundServiceTests()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "OutboxTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _publisher = Substitute.For<IPublisher>();
        _logger = Substitute.For<ILogger<OutboxProcessorBackgroundService>>();

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(ApplicationDbContext)).Returns(_dbContext);
        serviceProvider.GetService(typeof(IPublisher)).Returns(_publisher);
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _processor = new OutboxProcessorBackgroundService(_scopeFactory, _logger, TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenUnprocessedMessagesExist_PublishesNotificationAndMarksAsProcessed()
    {
        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Money totalAmount = new Money(250.00m, "USD");
        OrderCreatedEvent domainEvent = new OrderCreatedEvent(orderId, userId, totalAmount);

        OutboxMessage message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-1),
            Type = nameof(OrderCreatedEvent),
            Content = JsonSerializer.Serialize(domainEvent),
            ProcessedOnUtc = null,
            Error = null
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        int processedCount = await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        processedCount.Should().Be(1);

        await _publisher.Received(1).Publish(
            Arg.Is<object>((object n) =>
                n is DomainEventNotification<OrderCreatedEvent> &&
                ((DomainEventNotification<OrderCreatedEvent>)n).DomainEvent.OrderId == orderId),
            Arg.Any<CancellationToken>());

        OutboxMessage? updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedOnUtc.Should().NotBeNull();
        updatedMessage.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMessageIsAlreadyProcessed_SkipsProcessing()
    {
        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Money totalAmount = new Money(100.00m, "USD");
        OrderCreatedEvent domainEvent = new OrderCreatedEvent(orderId, userId, totalAmount);

        OutboxMessage alreadyProcessedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-10),
            Type = nameof(OrderCreatedEvent),
            Content = JsonSerializer.Serialize(domainEvent),
            ProcessedOnUtc = DateTime.UtcNow.AddMinutes(-5),
            Error = null
        };

        _dbContext.OutboxMessages.Add(alreadyProcessedMessage);
        await _dbContext.SaveChangesAsync();

        int processedCount = await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        processedCount.Should().Be(0);
        await _publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenPublisherThrowsException_CapturesErrorAndLeavesProcessedOnUtcNull()
    {
        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Money totalAmount = new Money(50.00m, "USD");
        OrderCreatedEvent domainEvent = new OrderCreatedEvent(orderId, userId, totalAmount);

        OutboxMessage message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-1),
            Type = nameof(OrderCreatedEvent),
            Content = JsonSerializer.Serialize(domainEvent),
            ProcessedOnUtc = null,
            Error = null
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        _publisher.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Downstream notification service unavailable"));

        int processedCount = await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        processedCount.Should().Be(0);

        OutboxMessage? updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedOnUtc.Should().BeNull();
        updatedMessage.Error.Should().NotBeNull();
        updatedMessage.Error.Should().Contain("Downstream notification service unavailable");
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMessageTypeIsUnknown_SetsErrorAndDoesNotPublish()
    {
        OutboxMessage message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-1),
            Type = "NonExistentDomainEvent",
            Content = "{}",
            ProcessedOnUtc = null,
            Error = null
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        int processedCount = await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        processedCount.Should().Be(0);
        await _publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());

        OutboxMessage? updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedOnUtc.Should().BeNull();
        updatedMessage.Error.Should().Contain("Unknown domain event type 'NonExistentDomainEvent'");
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenContentIsCorruptedJson_SetsErrorAndDoesNotPublish()
    {
        OutboxMessage message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-1),
            Type = nameof(OrderCreatedEvent),
            Content = "this-is-corrupted-json{",
            ProcessedOnUtc = null,
            Error = null
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        int processedCount = await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        processedCount.Should().Be(0);
        await _publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());

        OutboxMessage? updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedOnUtc.Should().BeNull();
        updatedMessage.Error.Should().Contain("Deserialization error");
    }
}
