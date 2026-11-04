using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Controllers;

public record UpdateOrderStatusRequest(OrderStatus Status);

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly PaymentService _payments;

    public OrdersController(OrderService orders, PaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List([FromQuery] OrderStatus? status,
        [FromQuery] OrderType? type, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await _orders.ListAsync(status, type, from?.ToUniversalTime(), to?.ToUniversalTime(), search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct) => Ok(await _orders.GetByIdAsync(id, ct));

    [HttpGet("kitchen")]
    public async Task<ActionResult<List<OrderDto>>> Kitchen([FromQuery] PreparationStation? station, CancellationToken ct) =>
        Ok(await _orders.KitchenQueueAsync(station, ct));

    [HttpGet("by-table/{tableId:guid}")]
    public async Task<ActionResult<OrderDto>> ByTable(Guid tableId, CancellationToken ct)
    {
        var order = await _orders.GetActiveByTableAsync(tableId, ct);
        return order is null ? NoContent() : Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken ct) =>
        Ok(await _orders.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct) => Ok(await _orders.UpdateStatusAsync(id, request.Status, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct) =>
        Ok(await _orders.CancelAsync(id, request.Reason, ct));

    [HttpPost("{id:guid}/move")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> Move(Guid id, [FromBody] MoveOrderRequest request, CancellationToken ct) =>
        Ok(await _orders.MoveToTableAsync(id, request.TableId, ct));

    [HttpPost("{sourceId:guid}/merge/{targetId:guid}")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<OrderDto>> Merge(Guid sourceId, Guid targetId, CancellationToken ct) =>
        Ok(await _orders.MergeAsync(sourceId, targetId, ct));

    [HttpPost("{id:guid}/checkout")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> Checkout(Guid id, [FromBody] CheckoutRequest request, CancellationToken ct) =>
        Ok(await _payments.CheckoutAsync(id, request, ct));

    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<OrderDto>> Refund(Guid id, [FromBody] RefundRequest request, CancellationToken ct) =>
        Ok(await _payments.RefundAsync(id, request, ct));
}
