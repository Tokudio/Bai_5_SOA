using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dbPath = Path.GetFullPath(builder.Configuration["DatabasePath"] ?? "data/products.db",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<ProductDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDb>();
    db.Database.EnsureCreated();
    if (!db.Products.Any())
    {
        db.Products.AddRange(
            new Product { Id = 1, Name = "Laptop Dell Inspiron", Price = 19000000, StockQuantity = 8 },
            new Product { Id = 2, Name = "Chuot khong day", Price = 300000, StockQuantity = 35 },
            new Product { Id = 3, Name = "Ban phim co", Price = 750000, StockQuantity = 20 },
            new Product { Id = 4, Name = "Tai nghe", Price = 1200000, StockQuantity = 15 }
        );
        db.SaveChanges();
    }
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "products" }));
app.MapGet("/api/products", async (ProductDb db) =>
    await db.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
app.MapGet("/api/products/{id:int}", async (int id, ProductDb db) =>
    await db.Products.FindAsync(id) is { } product ? Results.Ok(product) : Results.NotFound());
app.Run();

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
public sealed class ProductDb(DbContextOptions<ProductDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder mb) =>
        mb.Entity<Product>().Property(x => x.Price).HasConversion<string>();
}
