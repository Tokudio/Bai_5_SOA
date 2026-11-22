using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportingService.Contracts;
using ReportingService.Data;

namespace ReportingService.Controllers;

[ApiController]
[Route("api/reports/costs")]
public sealed class CostsController(ReportingDbContext db) : ControllerBase
{
    // Demo / classroom only. Protect with authentication and administrator role in production.
    [HttpPut("products/{productId:int}")]
    public async Task<IActionResult> SetCost(int productId, [FromBody] CostUpdateRequest request,
        CancellationToken ct)
    {
        if (request.UnitCost is null || request.UnitCost < 0)
            return BadRequest(new { error = "unitCost is required and must be non-negative" });
        var product = await db.Products.FindAsync([productId], ct);
        if (product is null) return NotFound(new { error = "Sync products first" });
        product.UnitCost = request.UnitCost.Value;

        // Backfill only missing historical costs for this class exercise.
        // Real accounting systems should use cost recorded at purchase/sale time.
        var lines = await db.OrderLines.Where(x => x.ProductId == productId &&
            x.UnitCostAtSale == null).ToListAsync(ct);
        foreach (var line in lines) line.UnitCostAtSale = request.UnitCost.Value;
        await db.SaveChangesAsync(ct);
        return Ok(new { productId, unitCost = request.UnitCost, backfilledItems = lines.Count });
    }
}
