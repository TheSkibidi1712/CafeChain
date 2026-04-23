using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class Add_AttendanceLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsFaceVerified = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Staffs_UserId",
                        column: x => x.UserId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6156), new DateTime(2026, 4, 15, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6134) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6159), new DateTime(2026, 4, 21, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6158) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6161), new DateTime(2026, 3, 23, 18, 47, 41, 49, DateTimeKind.Local).AddTicks(6160) });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_StoreId",
                table: "AttendanceLogs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_UserId",
                table: "AttendanceLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceLogs");

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3005), new DateTime(2026, 4, 15, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(2978) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3009), new DateTime(2026, 4, 21, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3009) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3012), new DateTime(2026, 3, 23, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3011) });
        }
    }
}
