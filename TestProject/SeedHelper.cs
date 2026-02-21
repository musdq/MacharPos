using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MacharPos.Web.Tests.TestInfrastructure;

public static class SeedHelper
{
    public static async Task<(Station station, Product lpg12_5, ApplicationUser admin, ApplicationUser cashier)>
        SeedMasakaAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Roles
        if (!await roles.RoleExistsAsync("Admin"))
            await roles.CreateAsync(new IdentityRole("Admin"));
        if (!await roles.RoleExistsAsync("Cashier"))
            await roles.CreateAsync(new IdentityRole("Cashier"));

        // Station
        var station = await db.Stations.FirstOrDefaultAsync(s => s.Name == "Masaka");
        if (station is null)
        {
            station = new Station { Id = Guid.NewGuid(), Name = "Masaka" };
            db.Stations.Add(station);
            await db.SaveChangesAsync();
        }

        // Category + Product (12.5kg)
        var cat = await db.ProductCategories.FirstOrDefaultAsync(c => c.Name == "LPG");
        if (cat is null)
        {
            cat = new ProductCategory { Id = Guid.NewGuid(), Name = "LPG" };
            db.ProductCategories.Add(cat);
            await db.SaveChangesAsync();
        }

        var lpg12_5 = await db.Products.FirstOrDefaultAsync(p => p.Name == "LPG 12.5kg");
        if (lpg12_5 is null)
        {
            lpg12_5 = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = cat.Id,
                Name = "LPG 12.5kg",
                DefaultUnitPrice = 1000m, // any test value
                EquivalentKg = 12.5m,
                IsActive = true
            };
            db.Products.Add(lpg12_5);
            await db.SaveChangesAsync();
        }

        // Active LPG Batch
        var activeBatch = await db.LpgBatches.FirstOrDefaultAsync(b =>
            b.StationId == station.Id && b.Status == LpgBatchStatus.Active);

        if (activeBatch is null)
        {
            activeBatch = new LpgBatch
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                Status = LpgBatchStatus.Active,
                QuantityKg = 30000m,
                SoldKg = 0m,
                CostPerKg = 800m,
                DateReceived = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
            };
            db.LpgBatches.Add(activeBatch);
            await db.SaveChangesAsync();
        }

        // Users
        async Task<ApplicationUser> EnsureUser(string email, string role)
        {
            var u = await users.FindByEmailAsync(email);
            if (u is null)
            {
                u = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    HomeStationId = station.Id
                };
                var created = await users.CreateAsync(u, "pass123"); // matches your relaxed password settings
                if (!created.Succeeded)
                    throw new Exception("Failed creating test user: " + string.Join(", ", created.Errors.Select(e => e.Description)));
            }

            if (!await users.IsInRoleAsync(u, role))
                await users.AddToRoleAsync(u, role);

            return u;
        }

        var admin = await EnsureUser("admin@machar.test", "Admin");
        var cashier = await EnsureUser("cashier@machar.test", "Cashier");

        return (station, lpg12_5, admin, cashier);
    }
}