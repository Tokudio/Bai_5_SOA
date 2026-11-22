using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dbPath = Path.GetFullPath(builder.Configuration["DatabasePath"] ?? "data/orders.db",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<OrderDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDb>();
    db.Database.EnsureCreated();
    if (!db.Orders.Any())
    {
        db.Orders.AddRange(
            new SaleOrder { Id = 1, CustomerName = "Nguyen Van A", Status = "Completed",
                CreatedAt = new DateTime(2026,11,18,8,0,0,DateTimeKind.Utc), TotalAmount = 19600000,
                Items = new() { new SaleItem { Id = 1, ProductId = 1, Quantity = 1, UnitPrice = 19000000 },
                                new SaleItem { Id = 2, ProductId = 2, Quantity = 2, UnitPrice = 300000 } } },
            new SaleOrder { Id = 2, CustomerName = "Tran Thi B", Status = "Paid",
                CreatedAt = new DateTime(2026,11,19,9,0,0,DateTimeKind.Utc), TotalAmount = 2700000,
                Items = new() { new SaleItem { Id = 3, ProductId = 3, Quantity = 2, UnitPrice = 750000 },
                                new SaleItem { Id = 4, ProductId = 4, Quantity = 1, UnitPrice = 1200000 } } },
            new SaleOrder { Id = 3, CustomerName = "Le Van C", Status = "Pending",
                CreatedAt = new DateTime(2026,11,20,10,0,0,DateTimeKind.Utc), TotalAmount = 300000,
                Items = new() { new SaleItem { Id = 5, ProductId = 2, Quantity = 1, UnitPrice = 300000 } } },
            new SaleOrder { Id = 4, CustomerName = "Pham Van D", Status = "Cancelled",
                CreatedAt = new DateTime(2026,11,21,11,0,0,DateTimeKind.Utc), TotalAmount = 19000000,
                Items = new() { new SaleItem { Id = 6, ProductId = 1, Quantity = 1, UnitPrice = 19000000 } } }
        );
        db.SaveChanges();
    }
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "orders" }));
app.MapGet("/api/orders", async (OrderDb db) =>
    await db.Orders.AsNoTracking().Include(o => o.Items).OrderBy(o => o.Id)
        .Select(o => new { o.Id, o.CustomerName, o.Status, o.CreatedAt, o.TotalAmount,
            Items = o.Items.Select(i => new { i.Id, i.ProductId, i.Quantity, i.UnitPrice }) })
        .ToListAsync());
app.MapGet("/api/orders/{id:int}", async (int id, OrderDb db) =>
{
    var order = await db.Orders.AsNoTracking().Include(o => o.Items)
        .Where(o => o.Id == id)
        .Select(o => new { o.Id, o.CustomerName, o.Status, o.CreatedAt, o.TotalAmount,
            Items = o.Items.Select(i => new { i.Id, i.ProductId, i.Quantity, i.UnitPrice }) })
        .SingleOrDefaultAsync();
    return order is null ? Results.NotFound() : Results.Ok(order);
});
app.Run();

public sealed class SaleOrder
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public List<SaleItem> Items { get; set; } = new();
}
public sealed class SaleItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public SaleOrder Order { get; set; } = null!;
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
public sealed class OrderDb(DbContextOptions<OrderDb> options) : DbContext(options)
{
    public DbSet<SaleOrder> Orders => Set<SaleOrder>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<SaleOrder>().Property(x => x.TotalAmount).HasConversion<string>();
        mb.Entity<SaleItem>().Property(x => x.UnitPrice).HasConversion<string>();
        mb.Entity<SaleOrder>().HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
    }
}
