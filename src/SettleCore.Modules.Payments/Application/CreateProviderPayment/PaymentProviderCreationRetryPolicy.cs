namespace SettleCore.Modules.Payments.Application.CreateProviderPayment;

public sealed class PaymentProviderCreationRetryPolicy
{
    public PaymentProviderCreationRetryPolicy(TimeSpan maximumAge)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumAge, TimeSpan.Zero);
        MaximumAge = maximumAge;
    }
    public TimeSpan MaximumAge { get; }
}
