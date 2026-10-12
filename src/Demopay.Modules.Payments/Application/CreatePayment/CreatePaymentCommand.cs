namespace Demopay.Modules.Payments.Application.CreatePayment;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency);