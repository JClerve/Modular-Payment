using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentSuccessPersistence
{
    Task SaveAsync(
        Payment payment,
        PaymentLedgerPostingIntent intent,
        CancellationToken cancellationToken = default);
}
