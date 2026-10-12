using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Ledger;

namespace SettleCore.IntegrationTests;

public sealed class PaymentLedgerCompositionTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public PaymentLedgerCompositionTests(
        WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void PaymentLedgerPostingPortResolvesFromCompositionRoot()
    {
        using var scope =
            factory.Services.CreateScope();

        var port =
            scope.ServiceProvider
                .GetRequiredService<IPaymentLedgerPostingPort>();

        Assert.IsType<PaymentLedgerPostingAdapter>(port);
    }
}