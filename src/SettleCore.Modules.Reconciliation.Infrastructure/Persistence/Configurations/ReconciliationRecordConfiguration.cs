using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Infrastructure.Persistence.Configurations;

public sealed class ReconciliationRecordConfiguration
    : IEntityTypeConfiguration<ReconciliationRecord>
{
    public void Configure(
        EntityTypeBuilder<ReconciliationRecord> builder)
    {
        builder.ToTable("reconciliation_records");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasColumnName("id");

        builder.Property(record => record.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.ExpectedAmount)
            .HasColumnName("expected_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.ActualAmount)
            .HasColumnName("actual_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.ExpectedCurrency)
            .HasColumnName("expected_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(record => record.ActualCurrency)
            .HasColumnName("actual_currency")
            .HasMaxLength(3)
            .IsRequired();
    }
}
