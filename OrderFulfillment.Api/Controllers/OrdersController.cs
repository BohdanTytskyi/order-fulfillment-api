using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;
using OrderFulfillment.Application.Orders.Queries.GetOrderById;

namespace OrderFulfillment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        OrderResponseDto order = await _sender.Send(new GetOrderByIdQuery(id), cancellationToken);
        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
    {
        Guid orderId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { id = orderId }, new { OrderId = orderId });
    }
}