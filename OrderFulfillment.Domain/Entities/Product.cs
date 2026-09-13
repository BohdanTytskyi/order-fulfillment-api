using OrderFulfillment.Domain.Common;
using OrderFulfillment.Domain.Exceptions;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Domain.Entities;

public class Product : Entity
{
    public string Name { get; private set; }
    public Money Price { get; private set; }
    public int AvailableQuantity { get; private set; }

    public Product(Guid id, string name, Money price, int initialQuantity) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
            
        if (initialQuantity < 0)
            throw new ArgumentException("Initial quantity cannot be negative.", nameof(initialQuantity));

        Name = name;
        Price = price;
        AvailableQuantity = initialQuantity;
    }

    protected Product() { Name = string.Empty; Price = Money.Zero(); }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Must reserve at least 1 item.", nameof(quantity));

        if (AvailableQuantity < quantity)
            throw new InsufficientStockException(Name, AvailableQuantity, quantity);

        AvailableQuantity -= quantity;
    }
    
    public void Restock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Must restock at least 1 item.", nameof(quantity));
            
        AvailableQuantity += quantity;
    }
}
