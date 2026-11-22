namespace ReportingService.Contracts;

public sealed class SourceProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

public sealed class SourceOrder
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public List<SourceOrderItem> Items { get; set; } = new();
}

public sealed class SourceOrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
