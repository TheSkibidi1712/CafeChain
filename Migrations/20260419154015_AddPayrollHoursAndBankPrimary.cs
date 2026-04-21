using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollHoursAndBankPrimary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PayrollHours",
                table: "StaffShifts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 1,
                column: "PayrollHours",
                value: null);

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 2,
                column: "PayrollHours",
                value: null);

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 3,
                column: "PayrollHours",
                value: null);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2909), new DateTime(2026, 4, 12, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2881) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2914), new DateTime(2026, 4, 18, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2913) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2916), new DateTime(2026, 3, 20, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2916) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayrollHours",
                table: "StaffShifts");

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9454), new DateTime(2026, 4, 12, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9437) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9458), new DateTime(2026, 4, 18, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9457) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9460), new DateTime(2026, 3, 20, 21, 41, 48, 490, DateTimeKind.Local).AddTicks(9459) });
        }
    }
}
