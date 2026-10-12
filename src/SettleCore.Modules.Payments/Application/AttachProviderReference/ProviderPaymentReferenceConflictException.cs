namespace SettleCore.Modules.Payments.Application.AttachProviderReference;

public sealed class ProviderPaymentReferenceConflictException
    : Exception
{
    public ProviderPaymentReferenceConflictException()
        : base("Payment already has a different provider payment reference.") { }

    public ProviderPaymentReferenceConflictException(
        Exception innerException)
        : base(
            "Provider payment reference is already attached to another payment.",
            innerException)
    {
    }
}
