using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderEventRetrySchedule : Migration
    {
        private static readonly string[] DueIndexColumns = ["provider", "next_attempt_at", "received_at", "event_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "payment_provider_event_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_event_receipts_due",
                table: "payment_provider_event_receipts",
                columns: DueIndexColumns,
                filter: "processed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_provider_event_receipts_due",
                table: "payment_provider_event_receipts");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "payment_provider_event_receipts");
        }
    }
}
