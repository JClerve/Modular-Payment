using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

public sealed record GetProviderPaymentRequest(PaymentId PaymentId,
    ProviderPaymentReference ProviderReference, long AmountMinorUnits, string Currency);
