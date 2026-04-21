using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthInsuranceAndDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffShifts_StaffId_ShiftId_WorkDate",
                table: "StaffShifts");

            migrationBuilder.DropColumn(
                name: "DependentCount",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "DependentTaxCode",
                table: "Staffs");

            migrationBuilder.AlterColumn<int>(
                name: "ShiftId",
                table: "StaffShifts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdHoc",
                table: "StaffShifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HealthInsuranceNumber",
                table: "Staffs",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Shifts",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeShift",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "StaffDependents",
                columns: table => new
                {
                    StaffDependentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDependents", x => x.StaffDependentId);
                    table.ForeignKey(
                        name: "FK_StaffDependents_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 1,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 2,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 3,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 4,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 5,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 6,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Shifts",
                keyColumn: "ShiftId",
                keyValue: 7,
                columns: new[] { "Duration", "IsFreeShift" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 1,
                column: "IsAdHoc",
                value: false);

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 2,
                column: "IsAdHoc",
                value: false);

            migrationBuilder.UpdateData(
                table: "StaffShifts",
                keyColumn: "StaffShiftId",
                keyValue: 3,
                column: "IsAdHoc",
                value: false);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 101,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 102,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 103,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 104,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 105,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 106,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 107,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 108,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 109,
                column: "HealthInsuranceNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 110,
                column: "HealthInsuranceNumber",
                value: null);

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

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaffId_ShiftId_WorkDate",
                table: "StaffShifts",
                columns: new[] { "StaffId", "ShiftId", "WorkDate" },
                unique: true,
                filter: "[ShiftId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDependents_StaffId",
                table: "StaffDependents",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffDependents");

            migrationBuilder.DropIndex(
                name: "IX_StaffShifts_StaffId_ShiftId_WorkDate",
                table: "StaffShifts");

            migrationBuilder.DropColumn(
                name: "IsAdHoc",
                table: "StaffShifts");

            migrationBuilder.DropColumn(
                name: "HealthInsuranceNumber",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsFreeShift",
                table: "Shifts");

            migrationBuilder.AlterColumn<int>(
                name: "ShiftId",
                table: "StaffShifts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DependentCount",
                table: "Staffs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DependentTaxCode",
                table: "Staffs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 101,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 102,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 103,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 104,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 105,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 106,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 107,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 108,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 109,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

            migrationBuilder.UpdateData(
                table: "Staffs",
                keyColumn: "StaffId",
                keyValue: 110,
                columns: new[] { "DependentCount", "DependentTaxCode" },
                values: new object[] { 0, null });

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

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaffId_ShiftId_WorkDate",
                table: "StaffShifts",
                columns: new[] { "StaffId", "ShiftId", "WorkDate" },
                unique: true);
        }
    }
}
