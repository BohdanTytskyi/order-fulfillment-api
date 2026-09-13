namespace OrderFulfillment.Application.Common.Exceptions;

public class OrderNotFoundException : NotFoundException
{
    public Guid OrderId { get; }

    public OrderNotFoundException(Guid orderId)
        : base($"Order '{orderId}' not found.")
    {
        OrderId = orderId;
    }
}
