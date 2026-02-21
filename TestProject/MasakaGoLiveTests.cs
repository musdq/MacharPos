using FluentAssertions;
using MacharPos.Core.Entities;
using MacharPos.Web.Controllers;
using MacharPos.Web.Data;
using MacharPos.Web.Services.HQ;
using MacharPos.Web.Tests.TestInfrastructure;
using MacharPos.Web.ViewModels.Sales;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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
        var ctrl = new SalesController(db, stationCtx, users, signIn);

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