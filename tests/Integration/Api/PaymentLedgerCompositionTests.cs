using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Demopay.Modules.Payments.Application.Abstractions;
using Demopay.Modules.Payments.Infrastructure.Integrations.Ledger;

namespace Demopay.IntegrationTests;

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