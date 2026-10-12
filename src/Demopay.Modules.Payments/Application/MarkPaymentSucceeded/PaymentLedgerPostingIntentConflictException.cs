using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed class PaymentLedgerPostingIntentConflictException(PaymentId paymentId)
    : InvalidOperationException($"Payment '{paymentId.Value}' already has a different ledger posting intent.");
