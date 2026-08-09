using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderClosureEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrderLineClosures",
                columns: table => new
                {
                    PurchaseOrderLineClosureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    ClosedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLineClosures", x => x.PurchaseOrderLineClosureId);
                    table.CheckConstraint("CK_PurchaseOrderLineClosures_ClosedBaseQuantity_Positive", "[ClosedBaseQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseOrderLineClosures_ClosedProcurementQuantity_Positive", "[ClosedProcurementQuantity] IS NULL OR [ClosedProcurementQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_ActorStaffId",
                table: "PurchaseOrderLineClosures",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_ProcurementUnitId",
                table: "PurchaseOrderLineClosures",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_PurchaseOrderLineId_CreatedAtUtc",
                table: "PurchaseOrderLineClosures",
                columns: new[] { "PurchaseOrderLineId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_RequestKey",
                table: "PurchaseOrderLineClosures",
                column: "RequestKey",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO PurchaseOrderLineClosures
                (
                    PurchaseOrderLineId,
                    ClosedBaseQuantity,
                    ClosedProcurementQuantity,
                    ProcurementUnitId,
                    Reason,
                    RequestKey,
                    PayloadHash,
                    ActorStaffId,
                    CreatedAtUtc
                )
                SELECT
                    line.PurchaseOrderLineId,
                    line.ClosedRemainingQuantity,
                    CASE WHEN line.ClosedProcurementQuantity > 0 THEN line.ClosedProcurementQuantity ELSE NULL END,
                    CASE WHEN line.ClosedProcurementQuantity > 0 THEN line.ProcurementUnitId ELSE NULL END,
                    line.CloseRemainingReason,
                    CONCAT('LEGACY-PO-CLOSE-', line.PurchaseOrderLineId),
                    LOWER(CONVERT(varchar(64), HASHBYTES(
                        'SHA2_256',
                        CONCAT(
                            line.PurchaseOrderLineId, '|',
                            CONVERT(varchar(50), line.ClosedRemainingQuantity), '|',
                            line.CloseRemainingReason)), 2)),
                    line.ClosedRemainingByStaffId,
                    line.ClosedRemainingAtUtc
                FROM PurchaseOrderLines line
                WHERE line.ClosedRemainingQuantity > 0
                  AND NULLIF(LTRIM(RTRIM(line.CloseRemainingReason)), '') IS NOT NULL
                  AND line.ClosedRemainingByStaffId IS NOT NULL
                  AND line.ClosedRemainingAtUtc IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderLineClosures");
        }
    }
}
