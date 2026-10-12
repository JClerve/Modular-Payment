using System.Diagnostics.Metrics;

namespace SettleCore.Modules.Payments.Infrastructure.Dispatch;

public sealed class PaymentProviderEventMetrics
{
    private readonly Counter<long> attempts, completed, deferred, failures, retrySchedulingFailures;
    public PaymentProviderEventMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(PaymentLedgerPostingMetrics.MeterName);
        attempts = meter.CreateCounter<long>("settlecore.provider_receipts.attempts", "{attempt}", "Selected receipt processing attempts.");
        completed = meter.CreateCounter<long>("settlecore.provider_receipts.completed", "{receipt}", "Successful processing outcomes, including processed replays.");
        deferred = meter.CreateCounter<long>("settlecore.provider_receipts.deferred", "{receipt}", "Receipts awaiting local prerequisites.");
        failures = meter.CreateCounter<long>("settlecore.provider_receipts.failures", "{failure}", "Processing exceptions excluding cancellation.");
        retrySchedulingFailures = meter.CreateCounter<long>("settlecore.provider_receipts.retry_scheduling_failures", "{failure}", "Retry write exceptions excluding cancellation.");
    }
    internal void RecordAttempt() => attempts.Add(1);
    internal void RecordCompleted() => completed.Add(1);
    internal void RecordDeferred() => deferred.Add(1);
    internal void RecordFailure() => failures.Add(1);
    internal void RecordRetrySchedulingFailure() => retrySchedulingFailures.Add(1);
}
