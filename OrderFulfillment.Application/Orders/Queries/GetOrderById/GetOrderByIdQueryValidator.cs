using FluentValidation;

namespace OrderFulfillment.Application.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(query => query.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
