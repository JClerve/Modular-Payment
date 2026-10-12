using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentProviderEventReceipt
{
    private PaymentProviderEventReceipt() { }
    public string Provider { get; private set; } = null!;
    public string EventId { get; private set; } = null!;
    public Guid PaymentId { get; private set; }
    public string ProviderReference { get; private set; } = null!;
    public long AmountMinorUnits { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public bool IsLiveMode { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public ProviderPaymentSucceededEvent ToEvent() => new(EventId,
        Domain.PaymentId.From(PaymentId), Domain.ProviderPaymentReference.Create(Provider, ProviderReference),
        AmountMinorUnits, Currency, OccurredAt, IsLiveMode);
}
