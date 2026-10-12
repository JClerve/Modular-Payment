namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed record MarkPaymentSucceededCommand(
    Guid PaymentId);