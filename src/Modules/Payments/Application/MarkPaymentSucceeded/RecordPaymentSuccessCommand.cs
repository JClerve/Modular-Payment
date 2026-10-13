namespace Demopay.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed record RecordPaymentSuccessCommand(
    Guid PaymentId,
    PaymentLedgerPostingInput PostingInput);
