using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class PaymentLedgerPostingEventConfiguration
    : IEntityTypeConfiguration<PaymentLedgerPostingEvent>
{
    public void Configure(EntityTypeBuilder<PaymentLedgerPostingEvent> builder)
    {
        builder.ToTable("payment_ledger_posting_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PaymentId).HasColumnName("payment_id")
            .HasConversion(id => id.Value, value => PaymentId.From(value));
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.Kind).HasColumnName("kind")
            .HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.HasOne<PaymentLedgerPostingIntent>().WithMany()
            .HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PaymentId, x.OccurredAt, x.Id });
        builder.HasIndex(x => new { x.PaymentId, x.Kind }).IsUnique()
            .HasFilter("\"kind\" IN ('IntentRecorded', 'PostingAcknowledged')");
    }
}
