using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// --------------------
// Database connection
// --------------------
// 1) Prefer standard connection string name: DefaultConnection

string? cs = Environment.GetEnvironmentVariable("DATABASE_URL");

// fallback to local appsettings for dev
if (string.IsNullOrWhiteSpace(cs))
    cs = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(cs))
    throw new Exception("No DB connection configured.");

if (cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    cs = cs.Replace("postgres://", "postgresql://");

var npgsql = new NpgsqlConnectionStringBuilder(cs)
{
    // internal Railway host is fine; prefer SSL-safe default
    SslMode = SslMode.Prefer,
    TrustServerCertificate = true
}.ToString();
Console.WriteLine("DB=" + new NpgsqlConnectionStringBuilder(npgsql).Host);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(npgsql));

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
