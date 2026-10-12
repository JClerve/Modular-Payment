namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderEventProcessor
{
    // False means a durable receipt or its local prerequisites are not yet available.
    Task<bool> ProcessAsync(string provider, string eventId, bool expectedLiveMode,
        CancellationToken cancellationToken = default);
}
