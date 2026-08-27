using OrderFulfillment.Domain.Common;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Domain.Entities;

public class OrderItem : Entity
{
    public Guid ProductId { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    internal OrderItem(Guid id, Guid productId, Money unitPrice, int quantity) : base(id)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        ProductId = productId;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    protected OrderItem() { UnitPrice = Money.Zero(); }

    public Money GetTotalPrice()
    {
        return new Money(UnitPrice.Amount * Quantity, UnitPrice.Currency);
    }
}
