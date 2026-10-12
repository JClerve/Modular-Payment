using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentLedgerPostingRetrySchedule : Migration
    {
        private static readonly string[] DueIndexColumns = ["status", "next_attempt_at", "payment_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "payment_ledger_posting_intents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_ledger_posting_intents_due",
                table: "payment_ledger_posting_intents",
                columns: DueIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_ledger_posting_intents_due",
                table: "payment_ledger_posting_intents");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "payment_ledger_posting_intents");
        }
    }
}
