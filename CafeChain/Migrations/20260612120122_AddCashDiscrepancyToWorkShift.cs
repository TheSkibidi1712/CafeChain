using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddCashDiscrepancyToWorkShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashDiscrepancy",
                table: "WorkShifts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 12, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8926), new DateTime(2026, 6, 5, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8910) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 27, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8931), new DateTime(2026, 6, 11, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8930) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 8, 11, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8933), new DateTime(2026, 5, 13, 19, 1, 20, 2, DateTimeKind.Local).AddTicks(8932) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashDiscrepancy",
                table: "WorkShifts");

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 12, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1136), new DateTime(2026, 6, 5, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1107) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 27, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1140), new DateTime(2026, 6, 11, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1139) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 8, 11, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1142), new DateTime(2026, 5, 13, 9, 38, 54, 21, DateTimeKind.Local).AddTicks(1142) });
        }
    }
}
