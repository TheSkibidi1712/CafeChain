using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class ADR0001_DropAvailableQtyCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventory_AvailableQty",
                table: "StoreInventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventory_ReservedQty_Valid",
                table: "StoreInventories");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 9, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3297), new DateTime(2026, 6, 2, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3274) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 24, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3302), new DateTime(2026, 6, 8, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3301) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 8, 8, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3304), new DateTime(2026, 5, 10, 16, 7, 56, 704, DateTimeKind.Local).AddTicks(3304) });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventory_AvailableQty",
                table: "StoreInventories",
                sql: "[AvailableQty] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventory_ReservedQty_Valid",
                table: "StoreInventories",
                sql: "[ReservedQty] <= [AvailableQty]");
        }
    }
}
