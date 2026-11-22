namespace ReportingService.Contracts;

public sealed record SyncResult(int ProductsSynced, int OrdersSynced, int ItemsSynced, string Message);

public sealed record MoneyTotals(
    int SoldQuantity, decimal Revenue, decimal? Cost, decimal? Profit, bool CostsComplete);

public sealed record DashboardReport(
    int ProductCount, int CurrentStockQuantity, int OrderCount, int SuccessfulOrderCount,
    MoneyTotals Totals, IReadOnlyList<int> ProductsMissingCost);

public sealed record ProductReport(
    int ProductId, string ProductName, int CurrentStockQuantity, decimal SalePrice, decimal? UnitCost,
    int SuccessfulOrderCount, MoneyTotals Totals);

public sealed record OrderItemReport(
    int ProductId, string ProductName, int Quantity, decimal UnitPrice,
    decimal Revenue, decimal? UnitCostAtSale, decimal? Cost, decimal? Profit);

public sealed record OrderReport(
    int OrderId, string CustomerName, DateTime CreatedAt, string Status, bool IsSuccessful,
    decimal OriginalOrderAmount, MoneyTotals Totals, IReadOnlyList<OrderItemReport> Items);

public sealed record CostUpdateRequest(decimal? UnitCost);
