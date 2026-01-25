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
var cs = builder.Configuration.GetConnectionString("DefaultConnection");

// 2) Fallback to Railway DATABASE_URL (postgresql://...)
if (string.IsNullOrWhiteSpace(cs))
    cs = Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(cs))
    throw new Exception("Database connection string not configured. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

if (cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    cs = cs.Replace("postgres://", "postgresql://");

// Build Npgsql conn string (add SSL-safe defaults)
var npgsql = new NpgsqlConnectionStringBuilder(cs)
{
    // Railway internal host usually works without SSL, but Prefer is safe.
    // If you ever see SSL errors, change Prefer -> Require.
    SslMode = SslMode.Prefer,
    TrustServerCertificate = true
}.ToString();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(npgsql));

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
