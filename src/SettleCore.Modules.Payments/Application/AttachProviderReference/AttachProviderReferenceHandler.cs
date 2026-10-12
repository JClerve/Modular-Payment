using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.AttachProviderReference;

public sealed class AttachProviderReferenceHandler(
    IPaymentRepository repository, IPaymentProviderReferencePersistence persistence)
{
    public async Task<AttachProviderReferenceResult?> HandleAsync(
        AttachProviderReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var paymentId =
            PaymentId.From(command.PaymentId);

        var payment = await repository.GetByIdAsync(
            paymentId,
            cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var providerReference =
            ProviderPaymentReference.Create(
                command.Provider,
                command.Reference);

        payment.AttachProviderReference(
            providerReference);

        if (!await persistence.AttachAsync(paymentId, providerReference, cancellationToken))
        {
            return null;
        }

        return new AttachProviderReferenceResult(
            payment.Id.Value,
            providerReference.Provider,
            providerReference.Reference);
    }
}