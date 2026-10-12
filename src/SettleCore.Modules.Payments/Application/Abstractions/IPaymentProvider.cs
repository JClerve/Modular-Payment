namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProvider
{
    Task<CreateProviderPaymentResult> CreatePaymentAsync(
        CreateProviderPaymentRequest request,
        CancellationToken cancellationToken = default);
}
