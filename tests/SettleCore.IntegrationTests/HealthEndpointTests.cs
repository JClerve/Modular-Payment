using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task UnavailableStoresPreventReadinessButNotLiveness()
    {
        const string unavailable = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1";
        using var factory = new Factory(unavailable, unavailable, unavailable);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task AllStoresMustHaveAppliedSchemasAndBeReachableBeforeReadinessSucceeds()
    {
        await using var payments = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var ledger = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var reconciliation = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await payments.StartAsync();
        await ledger.StartAsync();
        await reconciliation.StartAsync();
        using var factory = new Factory(payments.GetConnectionString(), ledger.GetConnectionString(), reconciliation.GetConnectionString());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        await scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>().Database.MigrateAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        await ledger.StopAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    private sealed class Factory(string payments, string ledger, string reconciliation) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            ReplaceContext<PaymentsDbContext>(services, payments);
            ReplaceContext<LedgerDbContext>(services, ledger);
            ReplaceContext<ReconciliationDbContext>(services, reconciliation);
        });
        private static void ReplaceContext<TContext>(IServiceCollection services, string connection) where TContext : DbContext
        {
            services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<TContext>();
            services.AddDbContext<TContext>(options => options.UseNpgsql(connection));
        }
    }
}
