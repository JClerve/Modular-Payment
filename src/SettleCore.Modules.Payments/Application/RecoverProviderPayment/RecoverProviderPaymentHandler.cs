using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.RecoverProviderPayment;

public sealed class RecoverProviderPaymentHandler(
    IPaymentRepository payments, IPaymentProviderReader provider, IPaymentProviderReferencePersistence persistence)
{
    public async Task<AttachProviderReferenceResult?> HandleAsync(AttachProviderReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var payment = await payments.GetByIdAsync(PaymentId.From(command.PaymentId), cancellationToken);
        if (payment is null) return null;
        var reference = ProviderPaymentReference.Create(command.Provider, command.Reference);
        if (payment.ProviderReference is { } linked && linked != reference)
            throw new ProviderPaymentReferenceConflictException();
        // Retrieve and correlate the original provider object; never call creation or infer completion.
        var verified = await provider.GetPaymentAsync(new GetProviderPaymentRequest(payment.Id, reference,
            PaymentAmountConversion.ToMinorUnits(payment.Amount, payment.Currency), payment.Currency), cancellationToken);
        if (verified.ProviderReference != reference)
            throw new InvalidOperationException("Provider recovery returned a different reference.");
        if (!await persistence.AttachAsync(payment.Id, reference, cancellationToken)) return null;
        return new(payment.Id.Value, reference.Provider, reference.Reference);
    }
}
