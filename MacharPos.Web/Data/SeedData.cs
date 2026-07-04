using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Data;

public static class SeedData
{
    public static async Task RunAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Stations
        if (!await db.Stations.AnyAsync())
        {
            db.Stations.AddRange(
                new Station { Name = "Masaka" },
                new Station { Name = "Borno" }
            );
            await db.SaveChangesAsync();
        }

        var masaka = await db.Stations.FirstAsync(s => s.Name.Contains("Masaka"));
        var borno = await db.Stations.FirstAsync(s => s.Name.Contains("Borno"));

        // Roles
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "StationManager", "Cashier", "Auditor" })
        {
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));
        }

        // Admin user
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = "admin@macharpos.local";
        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                HomeStationId = masaka.Id
            };
            await userMgr.CreateAsync(admin, "machar123");
            await userMgr.AddToRoleAsync(admin, "Admin");
        }

        // Example cashier (optional)
        var cashierEmail = "cashier@macharpos.local";
        var cashier = await userMgr.FindByEmailAsync(cashierEmail);
        if (cashier is null)
        {
            cashier = new ApplicationUser
            {
                UserName = cashierEmail,
                Email = cashierEmail,
                EmailConfirmed = true,
                HomeStationId = masaka.Id
            };
            await userMgr.CreateAsync(cashier, "machar123");
            await userMgr.AddToRoleAsync(cashier, "Cashier");
        }

       

        // Walk-in customer per station
        foreach (var st in await db.Stations.ToListAsync())
        {
            var exists = await db.Customers.AnyAsync(c => c.StationId == st.Id && c.Name == "Walk-in");
            if (!exists)
                db.Customers.Add(new Customer { StationId = st.Id, Name = "Walk-in" });

            // Categories + products (if empty)
            if (!await db.ProductCategories.AnyAsync())
            {
                var catRefills = new ProductCategory { Name = "LPG Refills" };
                var catCylinders = new ProductCategory { Name = "Cylinders" };
                var catAccessories = new ProductCategory { Name = "Accessories" };

                db.ProductCategories.AddRange(catRefills, catCylinders, catAccessories);
                await db.SaveChangesAsync();

                decimal[] sizes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12.5m, 25, 50];
                foreach (var s in sizes)
                {
                    db.Products.Add(new Product
                    {
                        CategoryId = catRefills.Id,
                        StationId = st.Id,
                        Name = $"LPG Refill – {s}kg",
                        UnitType = "Piece",
                        DefaultUnitPrice = 0m,
                        EquivalentKg = s
                    });
                }
             
            }
        }
        await db.SaveChangesAsync();
    }
}
