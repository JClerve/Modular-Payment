using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence.Configurations;

public sealed class LedgerAccountConfiguration
    : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(
        EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("ledger_accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(account => account.LedgerId)
            .HasColumnName("ledger_id")
            .IsRequired();

        builder.Property(account => account.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.HasIndex(account => account.LedgerId)
            .HasDatabaseName("ix_ledger_accounts_ledger_id");
    }
}