using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderCreationAttempts
{
    Task<DateTimeOffset> GetOrRecordAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
}
