using Microsoft.Extensions.Configuration;
using Demopay.Modules.Payments.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Demopay.Modules.Payments.Application.Abstractions;
using Demopay.Modules.Payments.Infrastructure.Persistence.Repositories;

namespace Demopay.IntegrationTests;

public sealed class PaymentLedgerPostingIntentRegistrationTests
{
    [Fact]
    public void ModuleRegistersScopedIntentRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payments"] =
                    "Host=localhost;Database=payments;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddPaymentsModule(configuration);
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<IPaymentLedgerPostingIntentRepository>();
        Assert.IsType<EfPaymentLedgerPostingIntentRepository>(first);
        Assert.Same(first, firstScope.ServiceProvider
            .GetRequiredService<IPaymentLedgerPostingIntentRepository>());
        Assert.NotSame(first, secondScope.ServiceProvider
            .GetRequiredService<IPaymentLedgerPostingIntentRepository>());
    }
}
