using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class PaymentProviderEventReceiptConfiguration : IEntityTypeConfiguration<PaymentProviderEventReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentProviderEventReceipt> builder)
    {
        builder.ToTable("payment_provider_event_receipts");
        builder.HasKey(x => new { x.Provider, x.EventId });
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100);
        builder.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(255);
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.ProviderReference).HasColumnName("provider_reference").HasMaxLength(255);
        builder.Property(x => x.AmountMinorUnits).HasColumnName("amount_minor_units");
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.IsLiveMode).HasColumnName("is_live_mode");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.HasIndex(x => new { x.Provider, x.NextAttemptAt, x.ReceivedAt, x.EventId })
            .HasFilter("processed_at IS NULL").HasDatabaseName("ix_provider_event_receipts_due");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.HasIndex(x => new { x.ReceivedAt, x.Provider, x.EventId }).HasFilter("processed_at IS NULL");
    }
}
