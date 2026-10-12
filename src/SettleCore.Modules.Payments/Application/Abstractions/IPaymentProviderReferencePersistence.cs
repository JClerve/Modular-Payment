using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderReferencePersistence
{
    Task<bool> AttachAsync(PaymentId paymentId, ProviderPaymentReference reference,
        CancellationToken cancellationToken = default);
}
