namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed class ProviderPaymentEventConflictException : Exception
{
    public ProviderPaymentEventConflictException() : base("Provider event identity has already been received with different evidence.")
    {
    }
}
