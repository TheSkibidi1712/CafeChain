using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class POS_V2_upd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PosTerminalId",
                table: "WorkShifts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<int>(type: "int", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceAuditLogs_Staffs_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK_InvoiceAuditLogs_Staffs_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
                });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 30, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6651), new DateTime(2026, 5, 24, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6628) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 15, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6654), new DateTime(2026, 5, 30, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6653) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 7, 30, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6656), new DateTime(2026, 5, 1, 22, 17, 49, 928, DateTimeKind.Local).AddTicks(6655) });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_CashierId",
                table: "InvoiceAuditLogs",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_SupervisorId",
                table: "InvoiceAuditLogs",
                column: "SupervisorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceAuditLogs");

            migrationBuilder.DropColumn(
                name: "PosTerminalId",
                table: "WorkShifts");

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
    }
}
