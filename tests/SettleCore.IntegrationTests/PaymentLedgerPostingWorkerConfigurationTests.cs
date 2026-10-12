using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SettleCore.Api.BackgroundTasks;

namespace SettleCore.IntegrationTests;

public sealed class PaymentLedgerPostingWorkerConfigurationTests
{
    [Fact]
    public void DefaultHostStartsWithPostingWorkerDisabled()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.False(factory.Services.GetRequiredService<IOptions<PaymentLedgerPostingWorkerOptions>>().Value.Enabled);
    }

    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("PollIntervalMilliseconds", "-1")]
    [InlineData("RetryDelaySeconds", "0")]
    public void InvalidOperationalSettingPreventsHostStartup(string setting, string value)
    {
        using var factory = new InvalidSettingsFactory(setting, value);
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("PollIntervalMilliseconds", "-1")]
    [InlineData("RetryDelaySeconds", "0")]
    public void InvalidOperationalSettingFailsExactOptionsValidation(string setting, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentLedgerPostingWorker:Enabled"] = "true",
            [$"PaymentLedgerPostingWorker:{setting}"] = value
        }).Build();
        var services = new ServiceCollection();
        services.AddPaymentLedgerPostingWorker(configuration);
        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<PaymentLedgerPostingWorkerOptions>>().Value);
        Assert.Equal(typeof(PaymentLedgerPostingWorkerOptions), exception.OptionsType);
        Assert.Contains("must be positive", exception.Message, StringComparison.Ordinal);
    }

    private sealed class InvalidSettingsFactory(string setting, string value) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["PaymentLedgerPostingWorker:Enabled"] = "true",
                    [$"PaymentLedgerPostingWorker:{setting}"] = value
                }));
        }
    }
}
