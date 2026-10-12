using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class PreparePaymentLedgerPostingEndpointTests
{
    [Fact]
    public async Task ExplicitPreparationIsValidatedDurableAndIdempotentWithoutSuccessOrDispatch()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new Factory(postgres.GetConnectionString());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        var response = await client.PostAsJsonAsync("/payments", new { amount = 12.34m, currency = "sgd" });
        var payment = Assert.IsType<CreatePaymentResult>(await response.Content.ReadFromJsonAsync<CreatePaymentResult>());
        var path = $"/payments/{payment.PaymentId}/ledger-posting/preparation";
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100);
        foreach (var invalid in new[] { input with { TransactionId = Guid.Empty }, input with { FeeAmountMinorUnits = 0 }, input with { FeeAmountMinorUnits = 1234 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, invalid)).StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentLedgerPostingPreparations.ToListAsync());
        var preparedResponse = await client.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.OK, preparedResponse.StatusCode);
        var prepared = Assert.IsType<PaymentLedgerPostingRequest>(await preparedResponse.Content.ReadFromJsonAsync<PaymentLedgerPostingRequest>());
        Assert.Equal(1234, prepared.GrossAmountMinorUnits);
        Assert.Equal(100, prepared.FeeAmountMinorUnits);
        Assert.Equal("SGD", prepared.Currency);
        Assert.Equal(input.TransactionId, prepared.TransactionId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input with { FeeAmountMinorUnits = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/payments/{Guid.NewGuid()}/ledger-posting/preparation", input)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var db = finalScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Assert.Equal(prepared, (await db.PaymentLedgerPostingPreparations.SingleAsync()).ToRequest());
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.SingleAsync()).Status);
        Assert.Empty(await db.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await db.PaymentLedgerPostingEvents.ToListAsync());
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<PaymentsDbContext>>();
            services.RemoveAll<DbContextOptions<PaymentsDbContext>>();
            services.RemoveAll<PaymentsDbContext>();
            services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connection));
        });
    }
}
