using OrderFulfillment.Domain.Common;
using OrderFulfillment.Domain.Events;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Domain.Entities;

public class Order : AggregateRoot
{
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal DiscountPercentage { get; private set; }

    public Order(Guid id, Guid userId, decimal discountPercentage = 0) : base(id)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
            throw new ArgumentException("Discount must be between 0 and 100", nameof(discountPercentage));

        UserId = userId;
        Status = OrderStatus.Pending;
        DiscountPercentage = discountPercentage;
    }

    protected Order() { }

    public void AddItem(Guid productId, Money unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Cannot add items to an order that is not pending.");

        OrderItem orderItem = new OrderItem(Guid.NewGuid(), productId, unitPrice, quantity);
        _items.Add(orderItem);
    }

    public Money CalculateTotal()
    {
        if (!_items.Any())
            return Money.Zero();

        string currency = _items.First().UnitPrice.Currency;
        Money total = Money.Zero(currency);
        
        foreach (OrderItem item in _items)
        {
            total += item.GetTotalPrice();
        }

        if (DiscountPercentage > 0)
        {
            total = total.ApplyDiscount(DiscountPercentage);
        }

        return total;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm order from status {Status}");
            
        if (!_items.Any())
            throw new InvalidOperationException("Cannot confirm an empty order.");

        Status = OrderStatus.Confirmed;

        Money totalAmount = CalculateTotal();
        AddDomainEvent(new OrderCreatedEvent(Id, UserId, totalAmount));
    }
    
    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot pay order from status {Status}");
            
        Status = OrderStatus.Paid;
    }
}
