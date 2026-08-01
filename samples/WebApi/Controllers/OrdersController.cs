using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderSummary>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("gateway")]
    public ActionResult<string> GetGateway([FromKeyedServices("stripe")] IPaymentGateway gateway) =>
        Ok(gateway.GetName());
}
