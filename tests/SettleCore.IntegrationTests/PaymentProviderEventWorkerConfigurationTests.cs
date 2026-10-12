using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SettleCore.Api.BackgroundTasks;

namespace SettleCore.IntegrationTests;

public sealed class PaymentProviderEventWorkerConfigurationTests
{
    [Fact]
    public void DefaultHostStartsWithProviderWorkerDisabled()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.False(factory.Services.GetRequiredService<IOptions<PaymentProviderEventWorkerOptions>>().Value.Enabled);
    }

    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("PollIntervalMilliseconds", "-1")]
    [InlineData("RetryDelaySeconds", "0")]
    [InlineData("Payments:Stripe:Enabled", "false")]
    [InlineData("Payments:Stripe:IsLiveMode", null)]
    public void InvalidEnabledWorkerConfigurationPreventsStartup(string setting, string? value)
    {
        using var factory = new Factory(setting, value);
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private sealed class Factory(string setting, string? value) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["PaymentProviderEventWorker:Enabled"] = "true",
                    ["Payments:Stripe:Enabled"] = "true",
                    ["Payments:Stripe:ApiKey"] = "sk_test_placeholder",
                    ["Payments:Stripe:WebhookSecret"] = "whsec_test_placeholder",
                    ["Payments:Stripe:SignatureToleranceSeconds"] = "300",
                    ["Payments:Stripe:IsLiveMode"] = "false"
                };
                values[setting.StartsWith("Payments:", StringComparison.Ordinal) ? setting : $"PaymentProviderEventWorker:{setting}"] = value;
                configuration.AddInMemoryCollection(values);
            });
        }
    }
}
