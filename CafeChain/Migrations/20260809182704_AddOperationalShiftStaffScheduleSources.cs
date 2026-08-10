using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalShiftStaffScheduleSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.CreateTable(
                name: "OperationalShiftScheduleSources",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    StaffShiftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShiftScheduleSources", x => new { x.OperationalShiftId, x.StaffShiftId });
                    table.ForeignKey(
                        name: "FK_OperationalShiftScheduleSources_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationalShiftScheduleSources_StaffShifts_StaffShiftId",
                        column: x => x.StaffShiftId,
                        principalTable: "StaffShifts",
                        principalColumn: "StaffShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "SourceScheduleShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftScheduleSources_StaffShiftId",
                table: "OperationalShiftScheduleSources",
                column: "StaffShiftId",
                unique: true);

            migrationBuilder.Sql(
                """
                ;WITH EffectiveSchedule AS
                (
                    SELECT
                        os.OperationalShiftId,
                        ss.StaffShiftId,
                        os.StartAtUtc AS SavedStartAtUtc,
                        os.EndAtUtc AS SavedEndAtUtc,
                        DATEADD(
                            SECOND,
                            DATEDIFF(SECOND, CAST('00:00:00' AS time), COALESCE(ss.CustomStartTime, s.StartTime)),
                            CAST(CAST(ss.WorkDate AS date) AS datetime2)) AS StartLocal,
                        DATEADD(
                            DAY,
                            CASE
                                WHEN COALESCE(ss.CustomEndTime, s.EndTime) <= COALESCE(ss.CustomStartTime, s.StartTime)
                                THEN 1 ELSE 0
                            END,
                            DATEADD(
                                SECOND,
                                DATEDIFF(SECOND, CAST('00:00:00' AS time), COALESCE(ss.CustomEndTime, s.EndTime)),
                                CAST(CAST(ss.WorkDate AS date) AS datetime2))) AS EndLocal
                    FROM OperationalShifts os
                    INNER JOIN StaffShifts ss
                        ON ss.ShiftId = os.SourceScheduleShiftId
                       AND CAST(ss.WorkDate AS date) = os.BusinessDate
                    INNER JOIN Shifts s ON s.ShiftId = ss.ShiftId
                    INNER JOIN StaffShiftStatuses status ON status.StaffShiftStatusId = ss.StatusId
                    INNER JOIN Staffs staff ON staff.StaffId = ss.StaffId
                    INNER JOIN Accounts account ON account.AccountId = staff.AccountId
                    WHERE os.CreationSource = 'StaffSchedule'
                      AND os.Status = 'Draft'
                      AND NOT EXISTS (
                          SELECT 1 FROM IceAllocations allocation
                          WHERE allocation.OperationalShiftId = os.OperationalShiftId)
                      AND NOT EXISTS (
                          SELECT 1 FROM OperationalShiftWorkShifts link
                          WHERE link.OperationalShiftId = os.OperationalShiftId)
                      AND s.Active = 1
                      AND status.Code <> 'CANCELLED'
                      AND staff.Active = 1
                      AND account.Active = 1
                ),
                UtcSchedule AS
                (
                    SELECT
                        OperationalShiftId,
                        StaffShiftId,
                        SavedStartAtUtc,
                        SavedEndAtUtc,
                        CONVERT(datetime2, (StartLocal AT TIME ZONE 'SE Asia Standard Time') AT TIME ZONE 'UTC') AS EffectiveStartAtUtc,
                        CONVERT(datetime2, (EndLocal AT TIME ZONE 'SE Asia Standard Time') AT TIME ZONE 'UTC') AS EffectiveEndAtUtc
                    FROM EffectiveSchedule
                ),
                DeterministicDrafts AS
                (
                    SELECT OperationalShiftId
                    FROM UtcSchedule
                    GROUP BY OperationalShiftId, SavedStartAtUtc, SavedEndAtUtc
                    HAVING MIN(EffectiveStartAtUtc) = MAX(EffectiveStartAtUtc)
                       AND MIN(EffectiveEndAtUtc) = MAX(EffectiveEndAtUtc)
                       AND MIN(EffectiveStartAtUtc) = SavedStartAtUtc
                       AND MIN(EffectiveEndAtUtc) = SavedEndAtUtc
                ),
                UnambiguousOwnership AS
                (
                    SELECT source.StaffShiftId
                    FROM UtcSchedule source
                    INNER JOIN DeterministicDrafts draft
                        ON draft.OperationalShiftId = source.OperationalShiftId
                    GROUP BY source.StaffShiftId
                    HAVING COUNT(DISTINCT source.OperationalShiftId) = 1
                )
                INSERT INTO OperationalShiftScheduleSources (OperationalShiftId, StaffShiftId)
                SELECT source.OperationalShiftId, source.StaffShiftId
                FROM UtcSchedule source
                INNER JOIN DeterministicDrafts draft
                    ON draft.OperationalShiftId = source.OperationalShiftId
                INNER JOIN UnambiguousOwnership ownership
                    ON ownership.StaffShiftId = source.StaffShiftId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalShiftScheduleSources");

            migrationBuilder.DropIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "SourceScheduleShiftId" },
                unique: true,
                filter: "[SourceScheduleShiftId] IS NOT NULL AND [Status] <> 'Cancelled'");
        }
    }
}
