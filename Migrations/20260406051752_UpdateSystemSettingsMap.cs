using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSystemSettingsMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingId);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "SettingId", "Description", "SettingKey", "SettingValue" },
                values: new object[] { 1, "Toạ độ trung tâm mặc định (VD: TPHCM - 10.8231, 106.6297)", "Map_Default_Center", "10.8231, 106.6297" });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 6, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5514), new DateTime(2026, 3, 30, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5496) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 4, 21, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5518), new DateTime(2026, 4, 5, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5518) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 5, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5521), new DateTime(2026, 3, 7, 12, 17, 51, 153, DateTimeKind.Local).AddTicks(5521) });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_SettingKey",
                table: "SystemSettings",
                column: "SettingKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 6, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3248), new DateTime(2026, 3, 30, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3226) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 4, 21, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3254), new DateTime(2026, 4, 5, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3253) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 5, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3259), new DateTime(2026, 3, 7, 0, 14, 31, 50, DateTimeKind.Local).AddTicks(3258) });
        }
    }
}
