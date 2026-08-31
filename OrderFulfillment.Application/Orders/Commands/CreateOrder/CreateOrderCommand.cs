using MediatR;

namespace OrderFulfillment.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(Guid UserId, Guid ProductId, int Quantity) : IRequest<Guid>;
