namespace OrderFulfillment.Application.Common.Exceptions;

public class ProductNotFoundException : NotFoundException
{
    public Guid ProductId { get; }

    public ProductNotFoundException(Guid productId)
        : base($"Product '{productId}' not found.")
    {
        ProductId = productId;
    }
}
