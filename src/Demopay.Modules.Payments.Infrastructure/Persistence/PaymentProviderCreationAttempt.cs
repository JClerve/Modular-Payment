using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentProviderCreationAttempt
{
    private PaymentProviderCreationAttempt() { }
    public PaymentId PaymentId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
}
