using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed record ProviderPaymentSucceededEvent(
    string EventId,
    PaymentId PaymentId,
    ProviderPaymentReference ProviderReference,
    long AmountMinorUnits,
    string Currency,
    DateTimeOffset OccurredAt,
    bool IsLiveMode);
