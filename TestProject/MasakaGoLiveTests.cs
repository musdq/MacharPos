using FluentAssertions;
using MacharPos.Core.Entities;
using MacharPos.Web.Controllers;
using MacharPos.Web.Data;
using MacharPos.Web.Services.HQ;
using MacharPos.Web.Services.Loyalty;
using MacharPos.Web.Tests.TestInfrastructure;
using MacharPos.Web.ViewModels.Sales;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace MacharPos.Web.Tests.GoLive;

public class MasakaGoLiveTests : IAsyncLifetime
{
    private CustomWebApplicationFactory? _factory;

    public async Task InitializeAsync()
    {
        // ✅ Uses your Development DB config
        _factory = new CustomWebApplicationFactory();

        // Seed core data + users once
        await SeedHelper.SeedMasakaAsync(_factory.Services);
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private static ClaimsPrincipal PrincipalFor(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static DashboardController BuildDashboardController(
        AppDbContext db,
        IHqDashboardService hq,
        Guid stationId,
        UserManager<MacharPos.Web.Auth.ApplicationUser> users,
        string userId,
        params string[] roles)
    {
        var stationCtx = new FakeStationContext(stationId);
        var ctrl = new DashboardController(db, stationCtx, hq);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = PrincipalFor(userId, roles)
            }
        };

        return ctrl;
    }

    private static SalesController BuildSalesController(
        AppDbContext db,
        Guid stationId,
        UserManager<MacharPos.Web.Auth.ApplicationUser> users,
        SignInManager<MacharPos.Web.Auth.ApplicationUser> signIn,
        string userId,
        params string[] roles)
    {
        var stationCtx = new FakeStationContext(stationId);
        var loyalty = new LoyaltyService();
        var ctrl = new SalesController(db, stationCtx, users, signIn, loyalty);

        var http = new DefaultHttpContext
        {
            User = PrincipalFor(userId, roles)
        };

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = http
        };

        // ✅ Fix: provide TempData so redirects / errors don’t NullRef
        ctrl.TempData = new TempDataDictionary(http, new InMemoryTempDataProvider());

        return ctrl;
    }

    private static async Task<Customer> EnsureNamedLoyaltyCustomerAsync(AppDbContext db, Guid stationId, string name = "Loyalty Test Customer")
    {
        var existing = await db.Customers.FirstOrDefaultAsync(c =>
            c.StationId == stationId &&
            c.Name == name);

        if (existing is not null)
            return existing;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            Name = name,
            Phone = "08000000000",
            IsActive = true,
            IsLoyaltyEnabled = true,
            LoyaltyStampCount = 0,
            LoyaltyAvailableRewards = 0,
            LoyaltyLifetimeEarned = 0,
            LoyaltyLifetimeRedeemed = 0
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    [Fact(DisplayName = "2.1 Normal LPG sale completes and deducts batch Kg")]
    public async Task Normal_Lpg_Sale_Completes_And_Deducts_Batch()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, admin, cashier) = await SeedHelper.SeedMasakaAsync(services);

        var beforeBatch = await db.LpgBatches.AsNoTracking()
            .FirstAsync(b => b.StationId == station.Id && b.Status == LpgBatchStatus.Active);

        var vm = new SaleCreateVm
        {
            PaymentMethod = PaymentMethod.Cash,
            Lines = new List<SaleLineVm>
            {
                new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
            }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var afterBatch = await db.LpgBatches.AsNoTracking()
            .FirstAsync(b => b.Id == beforeBatch.Id);

        afterBatch.SoldKg.Should().Be(beforeBatch.SoldKg + 12.5m);
        afterBatch.RemainingKg.Should().Be(beforeBatch.RemainingKg - 12.5m);
    }

    [Fact(DisplayName = "3.1 If no active LPG batch, sale is blocked")]
    public async Task No_Active_Batch_Blocks_Sale()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);

        var batch = await db.LpgBatches.FirstAsync(b => b.StationId == station.Id && b.Status == LpgBatchStatus.Active);
        batch.Status = LpgBatchStatus.Closed;
        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            PaymentMethod = PaymentMethod.Cash,
            Lines = new() { new SaleLineVm { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice } }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<ViewResult>();
        ctrl.ModelState.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "3.2 Selling beyond remaining Kg is blocked")]
    public async Task Insufficient_Batch_Kg_Blocks_Sale()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);

        var batch = await db.LpgBatches.FirstAsync(b => b.StationId == station.Id && b.Status == LpgBatchStatus.Active);
        batch.QuantityKg = 5m;
        batch.SoldKg = 0m;
        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            PaymentMethod = PaymentMethod.Cash,
            Lines = new() { new SaleLineVm { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice } }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<ViewResult>();
        ctrl.ModelState.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "6.2 After daily close, cashier cannot sell (non-admin)")]
    public async Task Closed_Day_Blocks_Cashier_Sales()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);

        var today = DateTime.Today;
        var closeDate = new DateTime(today.Year, today.Month, today.Day);

        var existing = await db.DailyCloses
            .FirstOrDefaultAsync(x => x.StationId == station.Id && x.CloseDate == closeDate);

        if (existing is null)
        {
            db.DailyCloses.Add(new DailyClose
            {
                StationId = station.Id,
                CloseDate = closeDate,
                IsClosed = true,
                TotalSales = 0,
                TotalTransactions = 0
            });
        }
        else
        {
            existing.IsClosed = true;
            existing.TotalSales = 0;
            existing.TotalTransactions = 0;
        }

        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            PaymentMethod = PaymentMethod.Cash,
            Lines = new() { new SaleLineVm { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice } }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ControllerName.Should().Be("DailyClose");
    }

    [Fact(DisplayName = "4.1 HQ Dashboard is admin-only (HTTP integration)")]
    public async Task HqDashboard_Is_AdminOnly()
    {
        var client = _factory!.CreateClient(new()
        {
            AllowAutoRedirect = false
        });

        var (_, _, admin, cashier) = await SeedHelper.SeedMasakaAsync(_factory.Services);

        var cashierReq = new HttpRequestMessage(HttpMethod.Get, "/hq/dashboard");
        cashierReq.Headers.Add("X-Test-UserId", cashier.Id);
        cashierReq.Headers.Add("X-Test-Roles", "Cashier");

        var cashierRes = await client.SendAsync(cashierReq);

        cashierRes.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Forbidden,
            System.Net.HttpStatusCode.Redirect
        );

        var adminReq = new HttpRequestMessage(HttpMethod.Get, "/hq/dashboard");
        adminReq.Headers.Add("X-Test-UserId", admin.Id);
        adminReq.Headers.Add("X-Test-Roles", "Admin");

        var adminRes = await client.SendAsync(adminReq);
        adminRes.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact(DisplayName = "4.2 Named customer earns 1 loyalty stamp on qualifying LPG refill")]
    public async Task Named_Customer_Earns_Loyalty_Stamp_On_Qualifying_Sale()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);
        var customer = await EnsureNamedLoyaltyCustomerAsync(db, station.Id);

        var before = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);

        var vm = new SaleCreateVm
        {
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            ApplyLoyaltyReward = false,
            Lines = new List<SaleLineVm>
        {
            new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
        }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var after = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);

        after.LoyaltyStampCount.Should().Be(before.LoyaltyStampCount + 1);
        after.LoyaltyAvailableRewards.Should().Be(before.LoyaltyAvailableRewards);
    }

    [Fact(DisplayName = "4.3 6th qualifying refill converts to 1 available loyalty reward")]
    public async Task Sixth_Refill_Converts_To_Available_Loyalty_Reward()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);
        var customer = await EnsureNamedLoyaltyCustomerAsync(db, station.Id);

        customer.LoyaltyStampCount = 5;
        customer.LoyaltyAvailableRewards = 0;
        customer.LoyaltyLifetimeEarned = 0;
        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            ApplyLoyaltyReward = false,
            Lines = new List<SaleLineVm>
        {
            new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
        }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var after = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);

        after.LoyaltyStampCount.Should().Be(0);
        after.LoyaltyAvailableRewards.Should().Be(1);
        after.LoyaltyLifetimeEarned.Should().Be(1);
    }

    [Fact(DisplayName = "4.4 Available loyalty reward applies 25 percent discount on next refill")]
    public async Task Loyalty_Reward_Applies_25Percent_Discount()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);
        var customer = await EnsureNamedLoyaltyCustomerAsync(db, station.Id);

        customer.LoyaltyStampCount = 0;
        customer.LoyaltyAvailableRewards = 1;
        customer.LoyaltyLifetimeRedeemed = 0;
        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            ApplyLoyaltyReward = true,
            Lines = new List<SaleLineVm>
        {
            new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
        }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var sale = await db.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync(s => s.StationId == station.Id && s.CustomerId == customer.Id);

        var after = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);

        sale.LoyaltyRewardApplied.Should().BeTrue();
        sale.LoyaltyDiscountAmount.Should().Be(lpg12_5.DefaultUnitPrice * 0.25m);

        after.LoyaltyAvailableRewards.Should().Be(0);
        after.LoyaltyLifetimeRedeemed.Should().Be(1);
    }

    [Fact(DisplayName = "4.5 Walk-in customer does not earn loyalty")]
    public async Task WalkIn_Customer_Does_Not_Earn_Loyalty()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);

        var walkIn = await db.Customers.FirstAsync(c =>
            c.StationId == station.Id &&
            c.Name == "Walk-in");

        var before = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == walkIn.Id);

        var vm = new SaleCreateVm
        {
            CustomerId = walkIn.Id,
            PaymentMethod = PaymentMethod.Cash,
            ApplyLoyaltyReward = false,
            Lines = new List<SaleLineVm>
        {
            new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
        }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var after = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == walkIn.Id);

        after.LoyaltyStampCount.Should().Be(before.LoyaltyStampCount);
        after.LoyaltyAvailableRewards.Should().Be(before.LoyaltyAvailableRewards);
    }

    [Fact(DisplayName = "4.6 Loyalty reward cannot be redeemed when customer has none")]
    public async Task Loyalty_Redeem_With_No_Available_Reward_Does_Not_Apply_Discount()
    {
        var services = _factory!.Services;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<MacharPos.Web.Auth.ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<MacharPos.Web.Auth.ApplicationUser>>();

        var (station, lpg12_5, _, cashier) = await SeedHelper.SeedMasakaAsync(services);
        var customer = await EnsureNamedLoyaltyCustomerAsync(db, station.Id, "No Reward Customer");

        customer.LoyaltyStampCount = 2;
        customer.LoyaltyAvailableRewards = 0;
        await db.SaveChangesAsync();

        var vm = new SaleCreateVm
        {
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            ApplyLoyaltyReward = true,
            Lines = new List<SaleLineVm>
        {
            new() { ProductId = lpg12_5.Id, Qty = 1m, UnitPrice = lpg12_5.DefaultUnitPrice }
        }
        };

        var ctrl = BuildSalesController(db, station.Id, users, signIn, cashier.Id, "Cashier");

        var result = await ctrl.New(vm);

        result.Should().BeOfType<RedirectToActionResult>();

        var sale = await db.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync(s => s.StationId == station.Id && s.CustomerId == customer.Id);

        var after = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);

        sale.LoyaltyRewardApplied.Should().BeFalse();
        sale.LoyaltyDiscountAmount.Should().Be(0m);
        after.LoyaltyAvailableRewards.Should().Be(0);
        after.LoyaltyStampCount.Should().Be(3);
    }
}

/// <summary>
/// Stub station context for controller-level tests.
/// </summary>
internal sealed class FakeStationContext : MacharPos.Web.Services.IStationContext
{
    private readonly Guid _stationId;

    public FakeStationContext(Guid stationId)
    {
        _stationId = stationId;
    }

    public Task<Guid> GetStationIdOrThrowAsync()
        => Task.FromResult(_stationId);
}

internal sealed class InMemoryTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object?> _store = new();

    public IDictionary<string, object?> LoadTempData(HttpContext context)
        => _store;

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        _store.Clear();
        foreach (var kv in values) _store[kv.Key] = kv.Value;
    }
}