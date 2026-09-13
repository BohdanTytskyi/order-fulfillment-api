using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Domain.Entities;

namespace OrderFulfillment.Application.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderResponseDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        Order? order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(request.OrderId);
        }

        List<OrderItemResponseDto> items = order.Items.Select((OrderItem item) => new OrderItemResponseDto(
            item.Id,
            item.ProductId,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency,
            item.Quantity,
            item.GetTotalPrice().Amount
        )).ToList();

        return new OrderResponseDto(
            order.Id,
            order.UserId,
            order.Status.ToString(),
            order.CalculateTotal().Amount,
            order.CalculateTotal().Currency,
            order.DiscountPercentage,
            items
        );
    }
}
