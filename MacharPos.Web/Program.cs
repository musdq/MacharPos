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
// Read from correct env var mapping
var cs = builder.Configuration.GetConnectionString("DefaultConnection");

// Fallback to direct env var (extra safety)
cs ??= Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
cs ??= Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(cs))
    throw new Exception("DB connection string not found. Set ConnectionStrings__DefaultConnection (Railway).");

// Clean common bad formatting
cs = cs.Trim();

// If you accidentally pasted JSON into the value, reject early with clear message
if (cs.StartsWith("{"))
    throw new Exception("Connection string looks like JSON. In Railway Variables, the VALUE must be only the URL, not JSON.");

// Remove accidental quotes if present
cs = cs.Trim('"');

Console.WriteLine("[DB] Raw prefix: " + (cs.Length > 30 ? cs.Substring(0, 30) : cs)); // safe debug

if (cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    cs = cs.Replace("postgres://", "postgresql://");

// If it's URL style, convert to Npgsql keyword style (this is the key fix)
if (cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    var b = new NpgsqlConnectionStringBuilder(cs)
    {
        SslMode = SslMode.Prefer,
        TrustServerCertificate = true
    };
    cs = b.ToString();
}

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

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
