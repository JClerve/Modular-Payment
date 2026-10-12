using Demopay.Modules.Payments.Application.MarkPaymentSucceeded;

namespace Demopay.Modules.Payments.Application.PreparePaymentLedgerPosting;

public sealed record PreparePaymentLedgerPostingCommand(Guid PaymentId, PaymentLedgerPostingInput PostingInput);
