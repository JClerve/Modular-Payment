using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdAsync(
        PaymentId id,
        CancellationToken cancellationToken = default);

    Task<Payment?> GetByProviderReferenceAsync(
        ProviderPaymentReference providerReference,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Payment payment,
        CancellationToken cancellationToken = default);
}
