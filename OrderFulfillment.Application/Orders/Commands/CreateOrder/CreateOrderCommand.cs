using MediatR;
using OrderFulfillment.Application.Common.Behaviors;

namespace OrderFulfillment.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(Guid UserId, Guid ProductId, int Quantity) : IRequest<Guid>, ILockableRequest
{
    public string LockKey => $"product:{ProductId}";
}