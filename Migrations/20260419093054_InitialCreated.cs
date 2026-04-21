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
                values: new object[] { new DateTime(2026, 5, 19, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8061), new DateTime(2026, 4, 12, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8034) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8066), new DateTime(2026, 4, 18, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8065) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8068), new DateTime(2026, 3, 20, 16, 30, 52, 211, DateTimeKind.Local).AddTicks(8067) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(94), new DateTime(2026, 4, 12, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(86) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(98), new DateTime(2026, 4, 18, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(97) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(100), new DateTime(2026, 3, 20, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(99) });
        }
    }
}
