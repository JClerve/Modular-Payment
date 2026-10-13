using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderReferencePersistence
{
    Task<bool> AttachAsync(PaymentId paymentId, ProviderPaymentReference reference,
        CancellationToken cancellationToken = default);
}
