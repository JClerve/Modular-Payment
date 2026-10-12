namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderReader
{
    // Return only after reference, local identity, amount, currency and configured mode match.
    // Mismatched or unverifiable evidence must throw without returning a client secret.
    Task<CreateProviderPaymentResult> GetPaymentAsync(GetProviderPaymentRequest request,
        CancellationToken cancellationToken = default);
}
