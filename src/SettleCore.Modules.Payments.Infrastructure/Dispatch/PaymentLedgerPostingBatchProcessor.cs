using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.DispatchPaymentLedgerPosting;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Dispatch;

public sealed partial class PaymentLedgerPostingBatchProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<PaymentLedgerPostingBatchProcessor> logger,
    PaymentLedgerPostingMetrics metrics)
{
    public async Task<int> ProcessAsync(int batchSize, TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryDelay, TimeSpan.Zero);
        PaymentId[] paymentIds;
        await using (var selection = scopeFactory.CreateAsyncScope())
        {
            var repository = selection.ServiceProvider
                .GetRequiredService<IPaymentLedgerPostingIntentRepository>();
            paymentIds = (await repository.GetPendingAsync(batchSize, cancellationToken))
                .Select(intent => intent.PaymentId).ToArray();
        }
        var completed = 0;
        foreach (var paymentId in paymentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<DispatchPaymentLedgerPostingHandler>();
            try
            {
                metrics.RecordAttempt();
                if (await handler.HandleAsync(new DispatchPaymentLedgerPostingCommand(paymentId.Value),
                    cancellationToken))
                {
                    completed++;
                    metrics.RecordCompleted();
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                metrics.RecordFailure();
                PostingFailed(logger, exception, paymentId.Value);
                var repository = scope.ServiceProvider
                    .GetRequiredService<IPaymentLedgerPostingIntentRepository>();
                try
                {
                    await repository.ScheduleRetryAsync(paymentId,
                        clock.GetUtcNow().Add(retryDelay), cancellationToken);
                }
                catch (Exception schedulingException) when (!cancellationToken.IsCancellationRequested)
                {
                    metrics.RecordRetrySchedulingFailure();
                    RetrySchedulingFailed(logger, schedulingException, paymentId.Value);
                }
            }
        }
        return completed;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment {PaymentId} retry scheduling failed; intent remains pending.")]
    private static partial void RetrySchedulingFailed(ILogger logger, Exception exception, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment {PaymentId} ledger posting failed; scheduling retry.")]
    private static partial void PostingFailed(ILogger logger, Exception exception, Guid paymentId);
}
