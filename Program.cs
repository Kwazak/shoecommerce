using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Endpoints;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Options;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MidtransOptions>(
    builder.Configuration.GetSection(MidtransOptions.SectionName));
builder.Services.Configure<WhatsAppGatewayOptions>(
    builder.Configuration.GetSection(WhatsAppGatewayOptions.SectionName));
builder.Services.Configure<WarehouseOptions>(
    builder.Configuration.GetSection(WarehouseOptions.SectionName));

builder.Services.AddHybridCache();

var dbProvider = builder.Configuration["Database:Provider"]?.Trim() ?? "Sqlite";
if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
    dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
    dbProvider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
{
    var pgConn = builder.Configuration.GetConnectionString("PostgresConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:PostgresConnection wajib diisi untuk provider PostgreSQL.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(pgConn));
}
else
{
    var defaultDbPath = Path.Combine(builder.Environment.ContentRootPath, "pabrik_sepatu.db");
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr) || connStr == "Data Source=pabrik_sepatu.db")
    {
        connStr = $"Data Source={defaultDbPath}";
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connStr));
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".dp-keys")))
    .SetApplicationName("KaryaJayaFootwear");

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("Midtrans");
builder.Services.AddHttpClient("WhatsApp");

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PricingEngineService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<IFileStorageService, LocalDiskFileStorageService>();
builder.Services.AddScoped<INotificationService, WhatsAppGatewayNotificationService>();
builder.Services.AddScoped<IPaymentService, MidtransPaymentService>();
builder.Services.AddScoped<OrderFulfillmentService>();
builder.Services.AddScoped<AdminAnalyticsService>();
builder.Services.AddHostedService<OrderLifecycleBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "KaryaJayaAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizePage("/Account/Orders");
    options.Conventions.AuthorizePage("/Account/Profile");
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapApplicationApiEndpoints();
app.MapWarehouseEndpoints();

app.Run();

public partial class Program { }
