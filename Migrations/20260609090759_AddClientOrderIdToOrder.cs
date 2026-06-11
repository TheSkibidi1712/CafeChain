using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOrderIdToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientOrderId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId_Unique",
                table: "Orders",
                column: "ClientOrderId",
                unique: true,
                filter: "[ClientOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ClientOrderId_Unique",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                table: "Orders");

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
    }
}
