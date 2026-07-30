using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalShiftScheduleSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts");

            migrationBuilder.AddColumn<string>(
                name: "CreationSource",
                table: "OperationalShifts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<int>(
                name: "SourceScheduleShiftId",
                table: "OperationalShifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_SourceScheduleShiftId",
                table: "OperationalShifts",
                column: "SourceScheduleShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_CreationSource",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "CreationSource" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Name" },
                unique: true,
                filter: "[CreationSource] = 'Manual' AND [Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "SourceScheduleShiftId" },
                unique: true,
                filter: "[SourceScheduleShiftId] IS NOT NULL AND [Status] <> 'Cancelled'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OperationalShifts_CreationSource",
                table: "OperationalShifts",
                sql: "([CreationSource] = 'Manual' AND [SourceScheduleShiftId] IS NULL) OR ([CreationSource] = 'StaffSchedule' AND [SourceScheduleShiftId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalShifts_Shifts_SourceScheduleShiftId",
                table: "OperationalShifts",
                column: "SourceScheduleShiftId",
                principalTable: "Shifts",
                principalColumn: "ShiftId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationalShifts_Shifts_SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_CreationSource",
                table: "OperationalShifts");

            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts");

            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OperationalShifts_CreationSource",
                table: "OperationalShifts");

            migrationBuilder.DropColumn(
                name: "CreationSource",
                table: "OperationalShifts");

            migrationBuilder.DropColumn(
                name: "SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Name" },
                unique: true);
        }
    }
}
