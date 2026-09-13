using MediatR;

namespace OrderFulfillment.Application.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderResponseDto>;
