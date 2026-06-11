using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddPosTerminalAndUpgradeAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 3, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7464), new DateTime(2026, 5, 27, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7439) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7468), new DateTime(2026, 6, 2, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7467) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 8, 2, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7471), new DateTime(2026, 5, 4, 21, 6, 0, 196, DateTimeKind.Local).AddTicks(7470) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 2, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(2001), new DateTime(2026, 5, 26, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(1970) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 17, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(2005), new DateTime(2026, 6, 1, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(2004) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 8, 1, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(2008), new DateTime(2026, 5, 3, 22, 27, 45, 632, DateTimeKind.Local).AddTicks(2007) });
        }
    }
}
