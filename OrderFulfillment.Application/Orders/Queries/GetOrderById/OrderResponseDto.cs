namespace OrderFulfillment.Application.Orders.Queries.GetOrderById;

public record OrderItemResponseDto(
    Guid Id,
    Guid ProductId,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal TotalPrice
);

public record OrderResponseDto(
    Guid Id,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    string Currency,
    decimal DiscountPercentage,
    IReadOnlyList<OrderItemResponseDto> Items
);
