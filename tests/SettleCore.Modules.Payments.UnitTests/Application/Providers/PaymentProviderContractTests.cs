using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.Providers;

public sealed class PaymentProviderContractTests
{
    [Fact]
    public async Task ProviderBoundaryReturnsNeutralDataAndForwardsCancellation()
    {
        var payment = Payment.Create(12.34m, "SGD");
        var request = new CreateProviderPaymentRequest(payment.Id, 1234, payment.Currency);
        using var cancellation = new CancellationTokenSource();
        var expected = new CreateProviderPaymentResult(
            ProviderPaymentReference.Create("test", "provider-reference"),
            ProviderPaymentStatus.RequiresAction,
            "test-client-secret");
        var provider = new StubProvider(expected);
        IPaymentProvider boundary = provider;

        var result = await boundary.CreatePaymentAsync(request, cancellation.Token);

        Assert.Same(expected, result);
        Assert.Same(request, provider.Request);
        Assert.Equal(cancellation.Token, provider.Token);
        Assert.Equal("test", result.ProviderReference.Provider);
        Assert.Equal("provider-reference", result.ProviderReference.Reference);
        Assert.Equal(ProviderPaymentStatus.RequiresAction, result.Status);
        Assert.Equal("test-client-secret", result.ClientSecret);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    private sealed class StubProvider(CreateProviderPaymentResult result) : IPaymentProvider
    {
        public CreateProviderPaymentRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<CreateProviderPaymentResult> CreatePaymentAsync(
            CreateProviderPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
