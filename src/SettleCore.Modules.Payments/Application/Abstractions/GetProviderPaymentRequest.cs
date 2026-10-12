using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed record GetProviderPaymentRequest(PaymentId PaymentId,
    ProviderPaymentReference ProviderReference, long AmountMinorUnits, string Currency);
