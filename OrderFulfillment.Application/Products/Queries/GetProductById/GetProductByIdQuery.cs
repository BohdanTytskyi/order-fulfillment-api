using MediatR;

namespace OrderFulfillment.Application.Products.Queries.GetProductById;

public record GetProductByIdQuery(Guid ProductId) : IRequest<ProductResponseDto>;
