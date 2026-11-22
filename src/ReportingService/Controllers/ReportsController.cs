using Microsoft.AspNetCore.Mvc;
using ReportingService.Contracts;
using ReportingService.Services;

namespace ReportingService.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ReportQueryService query, SyncService sync, ILogger<ReportsController> logger)
    : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardReport>> Summary(DateTime? from, DateTime? to,
        CancellationToken ct)
    {
        if (from > to) return BadRequest(new { error = "from must not be after to" });
        return Ok(await query.DashboardAsync(from, to, ct));
    }

    [HttpGet("products")]
    public async Task<ActionResult<List<ProductReport>>> Products(DateTime? from, DateTime? to,
        CancellationToken ct)
    {
        if (from > to) return BadRequest(new { error = "from must not be after to" });
        return Ok(await query.ProductsAsync(from, to, ct));
    }

    [HttpGet("products/{id:int}")]
    public async Task<ActionResult<ProductReport>> Product(int id, CancellationToken ct)
    {
        var product = (await query.ProductsAsync(null, null, ct)).FirstOrDefault(p => p.ProductId == id);
        return product is null ? NotFound(new { error = "Product not found" }) : Ok(product);
    }

    [HttpGet("orders")]
    public async Task<ActionResult<List<OrderReport>>> Orders(DateTime? from, DateTime? to,
        CancellationToken ct)
    {
        if (from > to) return BadRequest(new { error = "from must not be after to" });
        return Ok(await query.OrdersAsync(from, to, ct));
    }

    [HttpGet("orders/{id:int}")]
    public async Task<ActionResult<OrderReport>> Order(int id, CancellationToken ct)
    {
        var order = (await query.OrdersAsync(null, null, ct)).FirstOrDefault(o => o.OrderId == id);
        return order is null ? NotFound(new { error = "Order not found" }) : Ok(order);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncResult>> Sync(CancellationToken ct)
    {
        try { return Ok(await sync.RunAsync(ct)); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or
                                   System.Text.Json.JsonException or InvalidOperationException)
        {
            logger.LogWarning(e, "Failed to sync reporting snapshots");
            return StatusCode(502, new { error = "Unable to sync upstream services", detail = e.Message });
        }
    }
}
