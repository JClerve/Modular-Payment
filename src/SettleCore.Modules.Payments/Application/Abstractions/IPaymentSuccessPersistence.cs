using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentSuccessPersistence
{
    Task SaveAsync(
        Payment payment,
        PaymentLedgerPostingIntent intent,
        CancellationToken cancellationToken = default);
}
