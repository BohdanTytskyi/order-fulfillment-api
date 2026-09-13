namespace OrderFulfillment.Domain.Exceptions;

public class InsufficientStockException : DomainException
{
    public string ProductName { get; }
    public int AvailableQuantity { get; }
    public int RequestedQuantity { get; }

    public InsufficientStockException(string productName, int availableQuantity, int requestedQuantity)
        : base($"Not enough stock for product '{productName}'. Available: {availableQuantity}, Requested: {requestedQuantity}.")
    {
        ProductName = productName;
        AvailableQuantity = availableQuantity;
        RequestedQuantity = requestedQuantity;
    }
}
