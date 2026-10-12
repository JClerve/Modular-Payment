using System.Diagnostics.Metrics;

namespace SettleCore.Modules.Payments.Infrastructure.Dispatch;

public sealed class PaymentLedgerPostingMetrics
{
    public const string MeterName = "SettleCore.Payments";
    private readonly Counter<long> attempts;
    private readonly Counter<long> completed;
    private readonly Counter<long> failures;
    private readonly Counter<long> retrySchedulingFailures;

    public PaymentLedgerPostingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        attempts = meter.CreateCounter<long>("settlecore.payment_posting.attempts", "{attempt}",
            "Number of selected posting intents attempted by a batch.");
        completed = meter.CreateCounter<long>("settlecore.payment_posting.completed", "{intent}",
            "Number of successful batch dispatch outcomes, including already-posted replays.");
        failures = meter.CreateCounter<long>("settlecore.payment_posting.failures", "{failure}",
            "Number of posting or acknowledgment failures, excluding host cancellation.");
        retrySchedulingFailures = meter.CreateCounter<long>("settlecore.payment_posting.retry_scheduling_failures",
            "{failure}", "Number of failed retry schedule writes, excluding host cancellation.");
    }

    internal void RecordAttempt() => attempts.Add(1);
    internal void RecordCompleted() => completed.Add(1);
    internal void RecordFailure() => failures.Add(1);
    internal void RecordRetrySchedulingFailure() => retrySchedulingFailures.Add(1);
}
