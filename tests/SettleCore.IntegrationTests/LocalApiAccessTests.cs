using SettleCore.Api.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SettleCore.IntegrationTests;

public sealed class LocalApiAccessTests
{
    private const string TestKey = "test-only-operator-key-with-at-least-32-characters";

    [Theory]
    [InlineData("/payments")]
    [InlineData("/ledger/accounts")]
    [InlineData("/ledger/transactions")]
    [InlineData("/reconciliations")]
    public async Task SensitiveWritesRejectMissingAndWrongOperatorKey(string path)
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var missing = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        client.DefaultRequestHeaders.Add("X-SettleCore-Operator-Key", "wrong-key");
        using var wrong = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.DoesNotContain(TestKey, await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/payments/11111111-1111-1111-1111-111111111111")]
    [InlineData("/payments/11111111-1111-1111-1111-111111111111/provider-payment")]
    [InlineData("/ledger/transactions/11111111-1111-1111-1111-111111111111")]
    [InlineData("/reconciliations/11111111-1111-1111-1111-111111111111")]
    public async Task SensitiveReadsRejectMissingKeyBeforeDatabaseOrProviderResolution(string path)
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidKeyReachesHandlerButPlainHttpAndMultipleKeysAreRejected()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-SettleCore-Operator-Key", TestKey);
        using var valid = await client.PostAsJsonAsync("/payments", new { amount = 0, currency = "SGD" });
        Assert.Equal(HttpStatusCode.BadRequest, valid.StatusCode);
        using var plain = await client.PostAsJsonAsync("http://localhost/payments", new { amount = 0, currency = "SGD" });
        Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        client.DefaultRequestHeaders.Add("X-SettleCore-Operator-Key", TestKey);
        using var multiple = await client.PostAsJsonAsync("/payments", new { amount = 0, currency = "SGD" });
        Assert.Equal(HttpStatusCode.Unauthorized, multiple.StatusCode);
    }

    [Fact]
    public async Task LivenessAndSignedWebhookRouteDoNotRequireOperatorKey()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        using var disabledWebhook = await client.PostAsync("/payments/webhooks/stripe", null);
        Assert.Equal(HttpStatusCode.NotFound, disabledWebhook.StatusCode);
    }

    [Theory]
    [InlineData(true, null, "Development")]
    [InlineData(true, "too-short", "Development")]
    [InlineData(false, null, "Production")]
    public void UnsafeConfigurationFailsValidation(bool enabled, string? key, string environmentName)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LocalApiAccess:Enabled"] = enabled.ToString(), ["LocalApiAccess:OperatorKey"] = key }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalApiAccess(config, new HostEnvironment(environmentName));
        using var provider = services.BuildServiceProvider();
        var failure = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LocalApiAccessOptions>>().Value);
        Assert.Equal(typeof(LocalApiAccessOptions), failure.OptionsType);
        Assert.DoesNotContain(TestKey, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessControlsAreEnabledWithoutAnExplicitSetting()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LocalApiAccess:OperatorKey"] = TestKey }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalApiAccess(config, new HostEnvironment("Development"));
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<IOptions<LocalApiAccessOptions>>().Value.Enabled);
    }

    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SettleCore.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["LocalApiAccess:Enabled"] = "true", ["LocalApiAccess:OperatorKey"] = TestKey }));
    }
}
