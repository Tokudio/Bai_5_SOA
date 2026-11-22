using Microsoft.EntityFrameworkCore;
using ReportingService.Contracts;
using ReportingService.Data;
using ReportingService.Models;

namespace ReportingService.Services;

public sealed class ReportQueryService(ReportingDbContext db)
{
    private static readonly HashSet<string> SuccessfulStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Paid", "Completed", "Delivered" };

    private static bool Successful(OrderSnapshot order) =>
        SuccessfulStatuses.Contains(order.Status.Trim());

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static MoneyTotals Totals(IEnumerable<OrderLineSnapshot> lines)
    {
        var arr = lines.ToList();
        var revenue = Round(arr.Sum(x => x.Quantity * x.UnitPrice));
        var complete = arr.All(x => x.UnitCostAtSale.HasValue);
        decimal? cost = complete ? Round(arr.Sum(x => x.Quantity * x.UnitCostAtSale!.Value)) : null;
        return new MoneyTotals(arr.Sum(x => x.Quantity), revenue, cost,
            cost.HasValue ? Round(revenue - cost.Value) : null, complete);
    }

    private async Task<List<OrderSnapshot>> LoadOrders(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Include(x => x.Items).ToListAsync(ct);
        return orders.Where(o => (!from.HasValue || o.CreatedAt.Date >= from.Value.Date) &&
                                 (!to.HasValue || o.CreatedAt.Date <= to.Value.Date)).ToList();
    }

    public async Task<DashboardReport> DashboardAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().ToListAsync(ct);
        var orders = await LoadOrders(from, to, ct);
        var sold = orders.Where(Successful).ToList();
        var lines = sold.SelectMany(x => x.Items).ToList();
        var missing = lines.Where(x => x.UnitCostAtSale is null)
            .Select(x => x.ProductId).Distinct().OrderBy(x => x).ToList();
        return new DashboardReport(products.Count, products.Sum(x => x.StockQuantity),
            orders.Count, sold.Count, Totals(lines), missing);
    }

    public async Task<List<ProductReport>> ProductsAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var successful = (await LoadOrders(from, to, ct)).Where(Successful).ToList();
        return products.Select(p =>
        {
            var matching = successful.Where(o => o.Items.Any(i => i.ProductId == p.Id)).ToList();
            var lines = matching.SelectMany(o => o.Items.Where(i => i.ProductId == p.Id));
            return new ProductReport(p.Id, p.Name, p.StockQuantity, p.SalePrice,
                p.UnitCost, matching.Count, Totals(lines));
        }).ToList();
    }

    public async Task<List<OrderReport>> OrdersAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var orders = await LoadOrders(from, to, ct);
        return orders.OrderBy(x => x.Id).Select(o =>
        {
            var recognized = Successful(o);
            var lines = recognized ? o.Items : new List<OrderLineSnapshot>();
            var itemReports = o.Items.OrderBy(i => i.SourceItemId).Select(i =>
            {
                var revenue = recognized ? Round(i.UnitPrice * i.Quantity) : 0m;
                decimal? cost = !recognized ? 0m :
                    i.UnitCostAtSale.HasValue ? Round(i.UnitCostAtSale.Value * i.Quantity) : null;
                return new OrderItemReport(i.ProductId, i.ProductName, i.Quantity,
                    i.UnitPrice, revenue, i.UnitCostAtSale, cost,
                    cost.HasValue ? Round(revenue - cost.Value) : null);
            }).ToList();
            return new OrderReport(o.Id, o.CustomerName, o.CreatedAt, o.Status, recognized,
                o.SourceTotalAmount, Totals(lines), itemReports);
        }).ToList();
    }
}
