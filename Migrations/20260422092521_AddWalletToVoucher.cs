using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletToVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerVouchers",
                columns: table => new
                {
                    CustomerVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CollectedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UsedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerVouchers", x => x.CustomerVoucherId);
                    table.ForeignKey(
                        name: "FK_CustomerVouchers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "Description", "EndDate", "StartDate", "Title" },
                values: new object[] { null, new DateTime(2026, 5, 22, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7358), new DateTime(2026, 4, 15, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7338), null });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "Description", "EndDate", "StartDate", "Title" },
                values: new object[] { null, new DateTime(2026, 5, 7, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7362), new DateTime(2026, 4, 21, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7361), null });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "Description", "EndDate", "StartDate", "Title" },
                values: new object[] { null, new DateTime(2026, 6, 21, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7365), new DateTime(2026, 3, 23, 16, 25, 17, 198, DateTimeKind.Local).AddTicks(7365), null });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_CustomerId_VoucherId",
                table: "CustomerVouchers",
                columns: new[] { "CustomerId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_VoucherId",
                table: "CustomerVouchers",
                column: "VoucherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerVouchers");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Vouchers");

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
