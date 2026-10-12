using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;

public sealed class GetPaymentByProviderReferenceHandler(
    IPaymentRepository repository)
{
    public async Task<GetPaymentByProviderReferenceResult?> HandleAsync(
        GetPaymentByProviderReferenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var providerReference =
            ProviderPaymentReference.Create(
                query.Provider,
                query.ProviderReference);

        var payment =
            await repository.GetByProviderReferenceAsync(
                providerReference,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var attachedReference =
            payment.ProviderReference
            ?? throw new InvalidOperationException(
                "Payment provider reference is missing.");

        return new GetPaymentByProviderReferenceResult(
            payment.Id.Value,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            attachedReference.Provider,
            attachedReference.Reference);
    }
}