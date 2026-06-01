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
                values: new object[] { new DateTime(2026, 6, 29, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2751), new DateTime(2026, 5, 23, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2718) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 14, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2756), new DateTime(2026, 5, 29, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2755) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 29, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2760), new DateTime(2026, 4, 30, 20, 1, 55, 288, DateTimeKind.Local).AddTicks(2759) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 29, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4505), new DateTime(2026, 5, 23, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4489) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 14, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4508), new DateTime(2026, 5, 29, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4508) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 29, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4510), new DateTime(2026, 4, 30, 14, 36, 54, 304, DateTimeKind.Local).AddTicks(4510) });
        }
    }
}
