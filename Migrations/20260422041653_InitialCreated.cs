using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3539), new DateTime(2026, 4, 15, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3507) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3547), new DateTime(2026, 4, 21, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3546) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3549), new DateTime(2026, 3, 23, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3548) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 21, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5432), new DateTime(2026, 4, 14, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5409) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 6, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5438), new DateTime(2026, 4, 20, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5437) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 20, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5442), new DateTime(2026, 3, 22, 12, 59, 14, 321, DateTimeKind.Local).AddTicks(5441) });
        }
    }
}
