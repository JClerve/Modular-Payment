using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using SettleCore.Modules.Payments.Infrastructure.Dispatch;

namespace SettleCore.Api.BackgroundTasks;

internal sealed partial class PaymentProviderEventWorker(
    PaymentProviderEventBatchProcessor processor,
    IOptions<PaymentProviderEventWorkerOptions> options,
    IOptions<StripePaymentOptions> stripeOptions,
    TimeProvider clock,
    ILogger<PaymentProviderEventWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await processor.ProcessAsync("stripe", stripeOptions.Value.IsLiveMode!.Value, settings.BatchSize,
                        TimeSpan.FromSeconds(settings.RetryDelaySeconds), stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    BatchFailed(logger, exception);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(settings.PollIntervalMilliseconds),
                    clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown; pending receipts remain durable for the next process.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment provider receipt batch failed; retrying on the next poll.")]
    private static partial void BatchFailed(ILogger logger, Exception exception);
}
