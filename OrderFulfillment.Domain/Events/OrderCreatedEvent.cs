using OrderFulfillment.Domain.Common;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Domain.Events;

public class OrderCreatedEvent : IDomainEvent
{
    public Guid OrderId { get; }
    public Guid UserId { get; }
    public Money TotalAmount { get; }
    public DateTime OccurredOn { get; }

    public OrderCreatedEvent(Guid orderId, Guid userId, Money totalAmount)
    {
        OrderId = orderId;
        UserId = userId;
        TotalAmount = totalAmount;
        OccurredOn = DateTime.UtcNow;
    }
}
