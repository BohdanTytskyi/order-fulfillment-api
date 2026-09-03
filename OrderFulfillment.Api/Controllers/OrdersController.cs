using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;

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

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Guid orderId = await _sender.Send(command, cancellationToken);
            return Ok(new { OrderId = orderId });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { Error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { Error = exception.Message });
        }
    }
}