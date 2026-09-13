using FluentValidation;

namespace OrderFulfillment.Application.Products.Queries.GetProductById;

public class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(query => query.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required.");
    }
}
