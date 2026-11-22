namespace ReportingService.Models;

public sealed class ProductSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    // The purchasing/cost price is an independent reporting input.
    // Null means cost/profit cannot be calculated accurately yet.
    public decimal? UnitCost { get; set; }
}

public sealed class OrderSnapshot
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal SourceTotalAmount { get; set; }
    public List<OrderLineSnapshot> Items { get; set; } = new();
}

public sealed class OrderLineSnapshot
{
    public int Id { get; set; }
    public int SourceItemId { get; set; }
    public int OrderId { get; set; }
    public OrderSnapshot Order { get; set; } = null!;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCostAtSale { get; set; }
}
