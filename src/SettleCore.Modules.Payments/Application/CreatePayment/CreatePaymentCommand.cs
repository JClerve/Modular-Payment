namespace SettleCore.Modules.Payments.Application.CreatePayment;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency);