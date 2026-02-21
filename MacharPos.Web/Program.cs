using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Services.HQ;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// --------------------
// Database connection
// --------------------
// 1) Prefer standard connection string name: DefaultConnection
// Read from correct env var mapping

// Read connection string (Railway env var mapping)
var cs =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(cs))
    throw new Exception("DB connection string not found. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");

cs = cs.Trim().Trim('"');

//Console.WriteLine("[DB] Raw prefix: " + (cs.Length > 30 ? cs.Substring(0, 30) : cs));

string npgsqlConnString;

// If URL style, parse manually (works in all Npgsql versions)
if (cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
    cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
{
    // Normalize scheme
    if (cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        cs = "postgresql://" + cs.Substring("postgres://".Length);

    var uri = new Uri(cs);

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    var database = uri.AbsolutePath.TrimStart('/');
    database = Uri.UnescapeDataString(database);

    var b = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = database,
        Username = username,
        Password = password,

        // Railway-safe defaults
        SslMode = SslMode.Prefer,
        TrustServerCertificate = true
    };

    npgsqlConnString = b.ConnectionString;
}
else
{
    // Already keyword format
    var b = new NpgsqlConnectionStringBuilder(cs)
    {
        SslMode = SslMode.Prefer,
        TrustServerCertificate = true
    };
    npgsqlConnString = b.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(npgsqlConnString));


// --------------------
// Identity
// --------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireDigit = false;
        opt.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/account/login";
    opt.AccessDeniedPath = "/account/denied";
});

// --------------------
// Station context + misc
// --------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStationContext, StationContext>();
builder.Services.AddScoped<IHqDashboardService, HqDashboardService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --------------------
// Migrations + seeding
// --------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// SeedData can remain, but now it won't crash because DB is configured
await SeedData.RunAsync(app);

app.Run();
