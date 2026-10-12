using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

namespace SettleCore.Modules.Payments.Application.PreparePaymentLedgerPosting;

public sealed record PreparePaymentLedgerPostingCommand(Guid PaymentId, PaymentLedgerPostingInput PostingInput);
