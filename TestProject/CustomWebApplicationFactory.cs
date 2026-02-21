using MacharPos.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MacharPos.Web.Tests.TestInfrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ✅ Use your web project's normal dev config/DB
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // ✅ Replace auth with Test scheme (so we can simulate Cashier/Admin)
            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                opt.DefaultChallengeScheme = TestAuthHandler.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

            // Optional: ensure dev DB has latest migrations before tests
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }
}