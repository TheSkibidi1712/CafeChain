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
                values: new object[] { new DateTime(2026, 5, 24, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5463), new DateTime(2026, 4, 17, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5443) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 9, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5469), new DateTime(2026, 4, 23, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5469) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 23, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5478), new DateTime(2026, 3, 25, 18, 6, 31, 564, DateTimeKind.Local).AddTicks(5477) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 24, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8681), new DateTime(2026, 4, 17, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8672) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 9, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8685), new DateTime(2026, 4, 23, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8684) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 23, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8687), new DateTime(2026, 3, 25, 8, 55, 18, 416, DateTimeKind.Local).AddTicks(8686) });
        }
    }
}
