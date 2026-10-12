using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderCreationAttempts
{
    Task<DateTimeOffset> GetOrRecordAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
}
