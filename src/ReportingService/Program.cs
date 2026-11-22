using Microsoft.EntityFrameworkCore;
using ReportingService.Data;
using ReportingService.Services;

var builder = WebApplication.CreateBuilder(args);
var relativePath = builder.Configuration["DatabasePath"] ?? "data/reporting.db";
var dbPath = Path.GetFullPath(relativePath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<ReportingDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddHttpClient("products", c => c.BaseAddress = new Uri(
    builder.Configuration["UpstreamServices:Products"] ?? "http://localhost:5181/"));
builder.Services.AddHttpClient("orders", c => c.BaseAddress = new Uri(
    builder.Configuration["UpstreamServices:Orders"] ?? "http://localhost:5182/"));
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<ReportQueryService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    db.Database.EnsureCreated();
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "reporting" }));
app.MapControllers();
app.Run();
