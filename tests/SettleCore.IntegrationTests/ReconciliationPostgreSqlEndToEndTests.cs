using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Reconciliation.Application.CreateReconciliation;
using SettleCore.Modules.Reconciliation.Application.GetReconciliationById;
using SettleCore.Modules.Reconciliation.Domain;
using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class ReconciliationPostgreSqlEndToEndTests
{
    [Fact]
    public async Task PostReconciliationPersistsComparisonInPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory = new ReconciliationApiFactory(
            postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ReconciliationDbContext>();
            await context.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/reconciliations",
            new
            {
                expectedAmount = 100.00m,
                expectedCurrency = "sgd",
                actualAmount = 95.00m,
                actualCurrency = "usd"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<CreateReconciliationResult>(
            await response.Content.ReadFromJsonAsync<CreateReconciliationResult>());

        Assert.Equal(ReconciliationStatus.Mismatch, created.Status);
        Assert.Equal("SGD", created.ExpectedCurrency);
        Assert.Equal("USD", created.ActualCurrency);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ReconciliationDbContext>();

        var persisted = await verificationContext.ReconciliationRecords
            .AsNoTracking()
            .SingleAsync(record => record.Id == created.ReconciliationId);

        Assert.Equal(created.Status, persisted.Status);
        Assert.Equal(created.ExpectedAmount, persisted.ExpectedAmount);
        Assert.Equal(created.ActualAmount, persisted.ActualAmount);
        Assert.Equal(created.ExpectedCurrency, persisted.ExpectedCurrency);
        Assert.Equal(created.ActualCurrency, persisted.ActualCurrency);

        var getResponse = await client.GetAsync(
            $"/reconciliations/{created.ReconciliationId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = Assert.IsType<GetReconciliationByIdResult>(
            await getResponse.Content.ReadFromJsonAsync<GetReconciliationByIdResult>());
        Assert.Equal(created.ReconciliationId, fetched.ReconciliationId);
        Assert.Equal(created.Status, fetched.Status);
        Assert.Equal(created.ExpectedAmount, fetched.ExpectedAmount);
        Assert.Equal(created.ActualAmount, fetched.ActualAmount);
        Assert.Equal(created.ExpectedCurrency, fetched.ExpectedCurrency);
        Assert.Equal(created.ActualCurrency, fetched.ActualCurrency);
    }

    private sealed class ReconciliationApiFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<
                    IDbContextOptionsConfiguration<ReconciliationDbContext>>();
                services.RemoveAll<
                    DbContextOptions<ReconciliationDbContext>>();
                services.RemoveAll<ReconciliationDbContext>();

                services.AddDbContext<ReconciliationDbContext>(
                    options => options.UseNpgsql(connectionString));
            });
        }
    }
}
