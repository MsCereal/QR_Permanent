using DBPQRPermanent.Data;
using DBPQRPermanent.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Bind to Railway's PORT — required for Railway deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "DBP QR Permanent — Employee API",
        Version     = "v1",
        Description = "Manage employees and their permanent QR contact tokens."
    });
});

// SQLite path — /tmp is writable on Railway
var dbPath = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
    ? "dbpqr.db"
    : "/tmp/dbpqr.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<QRService>();

var app = builder.Build();

// Create DB + seed
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DatabaseSeeder.Seed(db);
    Console.WriteLine("[STARTUP] DB ready.");
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] DB error: {ex.Message}");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DBP QR Permanent API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "DBP QR API";
});

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

Console.WriteLine($"[STARTUP] Listening on http://0.0.0.0:{port}");
app.Run();
