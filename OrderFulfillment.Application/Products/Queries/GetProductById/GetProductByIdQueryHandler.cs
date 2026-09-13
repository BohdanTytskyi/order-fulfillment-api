using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Domain.Entities;

namespace OrderFulfillment.Application.Products.Queries.GetProductById;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductResponseDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        Product? product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync((Product p) => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        return new ProductResponseDto(
            product.Id,
            product.Name,
            product.Price.Amount,
            product.Price.Currency,
            product.AvailableQuantity
        );
    }
}
