using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentLedgerPostingPreparationRepository
{
    Task SaveAsync(PaymentLedgerPostingRequest request, CancellationToken cancellationToken = default);
    Task<PaymentLedgerPostingRequest?> GetByPaymentIdAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
}
