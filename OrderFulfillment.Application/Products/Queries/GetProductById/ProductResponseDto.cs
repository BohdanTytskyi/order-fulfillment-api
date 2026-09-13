namespace OrderFulfillment.Application.Products.Queries.GetProductById;

public record ProductResponseDto(
    Guid Id,
    string Name,
    decimal Price,
    string Currency,
    int AvailableQuantity
);
