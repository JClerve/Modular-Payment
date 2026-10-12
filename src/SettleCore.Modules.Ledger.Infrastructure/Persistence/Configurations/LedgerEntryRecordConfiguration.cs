using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryRecordConfiguration
    : IEntityTypeConfiguration<LedgerEntryRecord>
{
    public void Configure(
        EntityTypeBuilder<LedgerEntryRecord> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired();

        builder.Property(entry => entry.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(entry => entry.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entry => entry.AmountMinorUnits)
            .HasColumnName("amount_minor_units")
            .IsRequired();

        builder.HasIndex(entry => entry.TransactionId)
            .HasDatabaseName(
                "ix_ledger_entries_transaction_id");

        builder.HasIndex(entry => entry.AccountId)
            .HasDatabaseName(
                "ix_ledger_entries_account_id");

        builder.HasOne<LedgerAccount>()
            .WithMany()
            .HasForeignKey(entry => entry.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}