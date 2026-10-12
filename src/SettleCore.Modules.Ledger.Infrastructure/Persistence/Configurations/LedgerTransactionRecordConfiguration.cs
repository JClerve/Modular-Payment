using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence.Configurations;

public sealed class LedgerTransactionRecordConfiguration
    : IEntityTypeConfiguration<LedgerTransactionRecord>
{
    public void Configure(
        EntityTypeBuilder<LedgerTransactionRecord> builder)
    {
        builder.ToTable("ledger_transactions");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(transaction => transaction.LedgerId)
            .HasColumnName("ledger_id")
            .IsRequired();

        builder.HasIndex(transaction => transaction.LedgerId)
            .HasDatabaseName("ix_ledger_transactions_ledger_id");

        builder.HasMany(transaction => transaction.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}