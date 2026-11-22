using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReportingService.Contracts;
using ReportingService.Data;
using ReportingService.Models;

namespace ReportingService.Services;

public sealed class SyncService(ReportingDbContext db, IHttpClientFactory clients)
{
    public async Task<SyncResult> RunAsync(CancellationToken ct)
    {
        // Call upstream APIs through HTTP; NEVER read the other services' databases.
        var productClient = clients.CreateClient("products");
        var orderClient = clients.CreateClient("orders");
        var products = await productClient.GetFromJsonAsync<List<SourceProduct>>("api/products", ct)
            ?? throw new InvalidOperationException("Product API returned an empty response.");
        var orders = await orderClient.GetFromJsonAsync<List<SourceOrder>>("api/orders", ct)
            ?? throw new InvalidOperationException("Order API returned an empty response.");

        Validate(products, orders);
        // Fetch and validate everything before modifying local reporting data.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existingProducts = await db.Products.ToDictionaryAsync(x => x.Id, ct);
        foreach (var p in products)
        {
            if (!existingProducts.TryGetValue(p.Id, out var snapshot))
            {
                snapshot = new ProductSnapshot { Id = p.Id };
                db.Products.Add(snapshot);
                existingProducts.Add(p.Id, snapshot);
            }
            snapshot.Name = p.Name;
            snapshot.StockQuantity = p.StockQuantity;
            snapshot.SalePrice = p.Price;
            // Preserve reporting-owned unit cost.
        }
        // Historical orders may mention products no longer listed by Product API.
        foreach (var item in orders.SelectMany(o => o.Items))
        {
            if (!existingProducts.ContainsKey(item.ProductId))
            {
                var oldProduct = new ProductSnapshot
                {
                    Id = item.ProductId,
                    Name = $"Product #{item.ProductId} (removed from source)",
                    StockQuantity = 0
                };
                db.Products.Add(oldProduct);
                existingProducts.Add(item.ProductId, oldProduct);
            }
        }
        await db.SaveChangesAsync(ct);

        var oldCosts = await db.OrderLines.AsNoTracking().ToDictionaryAsync(
            x => new { x.OrderId, x.SourceItemId }, x => x.UnitCostAtSale, ct);
        // Replace imported orders atomically; previous unit-cost snapshots are retained.
        db.OrderLines.RemoveRange(db.OrderLines);
        db.Orders.RemoveRange(db.Orders);
        await db.SaveChangesAsync(ct);

        foreach (var source in orders)
        {
            var order = new OrderSnapshot
            {
                Id = source.Id,
                CustomerName = source.CustomerName,
                Status = source.Status,
                CreatedAt = source.CreatedAt,
                SourceTotalAmount = source.TotalAmount
            };
            foreach (var item in source.Items)
            {
                var key = new { OrderId = source.Id, SourceItemId = item.Id };
                decimal? cost = oldCosts.TryGetValue(key, out var previousCost)
                    ? previousCost : existingProducts[item.ProductId].UnitCost;
                order.Items.Add(new OrderLineSnapshot
                {
                    SourceItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = existingProducts[item.ProductId].Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UnitCostAtSale = cost
                });
            }
            db.Orders.Add(order);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new SyncResult(products.Count, orders.Count,
            orders.Sum(x => x.Items.Count), "Synchronized into independent reporting database.");
    }

    private static void Validate(List<SourceProduct> products, List<SourceOrder> orders)
    {
        if (products.Any(x => x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name) ||
            x.Price < 0 || x.StockQuantity < 0))
            throw new InvalidOperationException("Product API sent invalid product data.");
        if (products.Select(x => x.Id).Distinct().Count() != products.Count)
            throw new InvalidOperationException("Product API sent duplicate product IDs.");
        if (orders.Select(x => x.Id).Distinct().Count() != orders.Count)
            throw new InvalidOperationException("Order API sent duplicate order IDs.");
        foreach (var order in orders)
        {
            if (order.Id <= 0 || order.Items is null || string.IsNullOrWhiteSpace(order.Status) ||
                order.TotalAmount < 0 || order.Items.Any(x =>
                    x.Id <= 0 || x.ProductId <= 0 || x.Quantity <= 0 || x.UnitPrice < 0) ||
                order.Items.Select(x => x.Id).Distinct().Count() != order.Items.Count)
                throw new InvalidOperationException($"Invalid order or items for order {order.Id}.");
        }
    }
}
