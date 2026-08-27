namespace OrderFulfillment.Domain.Entities;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Paid = 2,
    Shipped = 3,
    Cancelled = 4
}
