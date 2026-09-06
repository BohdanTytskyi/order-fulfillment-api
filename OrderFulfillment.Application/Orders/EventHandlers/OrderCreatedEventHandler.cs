using MediatR;
using Microsoft.Extensions.Logging;
using OrderFulfillment.Application.Common.Events;
using OrderFulfillment.Domain.Events;

namespace OrderFulfillment.Application.Orders.EventHandlers;

public class OrderCreatedEventHandler : INotificationHandler<DomainEventNotification<OrderCreatedEvent>>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DomainEventNotification<OrderCreatedEvent> notification, CancellationToken cancellationToken)
    {
        OrderCreatedEvent domainEvent = notification.DomainEvent;

        _logger.LogInformation(
            "Order {OrderId} was created for User {UserId} with total amount {Amount} {Currency}. Occurred at: {OccurredOnUtc}",
            domainEvent.OrderId,
            domainEvent.UserId,
            domainEvent.TotalAmount.Amount,
            domainEvent.TotalAmount.Currency,
            domainEvent.OccurredOn);

        return Task.CompletedTask;
    }
}
