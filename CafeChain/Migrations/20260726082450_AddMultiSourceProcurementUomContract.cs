using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiSourceProcurementUomContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedForStoreId",
                table: "RestockRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForecastEvidence",
                table: "RestockRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NeedByDate",
                table: "RestockRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "RestockRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedProcurementQuantity",
                table: "RestockRequests",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReferenceId",
                table: "RestockRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "RestockRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<string>(
                name: "SourcingDecision",
                table: "RestockRequests",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcingStatus",
                table: "RestockRequests",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "UNALLOCATED");

            migrationBuilder.AddColumn<decimal>(
                name: "TargetStockProcurementQuantity",
                table: "RestockRequests",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MasterPurchaseOrderId",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseOrderReceiptPostings",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryPostingBaseQuantity",
                table: "PurchaseOrderReceiptPostings",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcurementToInventoryFactor",
                table: "PurchaseOrderReceiptPostings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedProcurementQuantity",
                table: "PurchaseOrderReceiptPostings",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPackQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosedProcurementQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InventoryBaseUnitId",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryPostingBaseQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedPackQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedProcurementQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackSizeProcurementQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcurementToInventoryFactor",
                table: "PurchaseOrderLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseAdviceLineId",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedProcurementQuantity",
                table: "PurchaseOrderLineAllocations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DemandCoveredProcurementQuantity",
                table: "PurchaseOrderLineAllocations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "PurchaseOrderLineAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderLineAllocations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DemandCoveredProcurementQuantity",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "PurchaseOrderBatchLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalProcurementQuantity",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseAdviceLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedToPoProcurementQuantity",
                table: "PurchaseAdviceLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosedProcurementQuantity",
                table: "PurchaseAdviceLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "PurchaseAdviceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedProcurementQuantity",
                table: "PurchaseAdviceLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestockSourcingAllocationId",
                table: "PurchaseAdviceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsLoosePurchase",
                table: "IngredientSuppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPackQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedProcurementQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InventoryBaseUnitId",
                table: "BranchReceiptLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryPostingBaseQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcurementToInventoryFactor",
                table: "BranchReceiptLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementUnitId",
                table: "BranchReceiptLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedPackQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedProcurementQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedProcurementQuantity",
                table: "BranchReceiptLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RestockSourcingAllocations",
                columns: table => new
                {
                    RestockSourcingAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    DecisionType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentLineId = table.Column<int>(type: "int", nullable: true),
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    ProductionRunId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockSourcingAllocations", x => x.RestockSourcingAllocationId);
                    table.CheckConstraint("CK_RestockSourcingAllocations_ActivePurchaseLink", "[Status] NOT IN ('ACTIVE','PENDING_PURCHASE') OR [DecisionType] <> 'PURCHASE' OR [PurchaseAdviceLineId] IS NOT NULL OR [PurchaseOrderLineId] IS NOT NULL OR [Status] = 'PENDING_PURCHASE'");
                    table.CheckConstraint("CK_RestockSourcingAllocations_Decision", "[DecisionType] IN ('TRANSFER','PURCHASE','PRODUCTION','REJECT')");
                    table.CheckConstraint("CK_RestockSourcingAllocations_Quantity", "[ProcurementQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_PurchaseAdviceLines_PurchaseAdviceLineId",
                        column: x => x.PurchaseAdviceLineId,
                        principalTable: "PurchaseAdviceLines",
                        principalColumn: "PurchaseAdviceLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Staffs_ReleasedByStaffId",
                        column: x => x.ReleasedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
UPDATE RestockRequests
SET CreatedForStoreId = StoreId,
    RequestedProcurementQuantity = RequestedQuantity
WHERE CreatedForStoreId IS NULL
  AND RequestedProcurementQuantity IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_CreatedForStoreId",
                table: "RestockRequests",
                column: "CreatedForStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_ProcurementUnitId",
                table: "RestockRequests",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_StoreId_SourceType_Status",
                table: "RestockRequests",
                columns: new[] { "StoreId", "SourceType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_StoreId_SourcingStatus",
                table: "RestockRequests",
                columns: new[] { "StoreId", "SourcingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_MasterPurchaseOrderId",
                table: "PurchaseOrders",
                column: "MasterPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_InventoryBaseUnitId",
                table: "PurchaseOrderLines",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ProcurementUnitId",
                table: "PurchaseOrderLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseAdviceLineId",
                table: "PurchaseOrderLines",
                column: "PurchaseAdviceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_ProcurementUnitId",
                table: "PurchaseOrderLineAllocations",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_ProcurementUnitId",
                table: "PurchaseOrderBatchLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_ProcurementUnitId",
                table: "PurchaseAdviceLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines",
                column: "RestockSourcingAllocationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseAdviceLines_ProcurementRequestedPositive",
                table: "PurchaseAdviceLines",
                sql: "[RequestedProcurementQuantity] IS NULL OR [RequestedProcurementQuantity] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_InventoryBaseUnitId",
                table: "BranchReceiptLines",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_ProcurementUnitId",
                table: "BranchReceiptLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_CreatedByStaffId",
                table: "RestockSourcingAllocations",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_InventoryTransferId",
                table: "RestockSourcingAllocations",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ProcurementUnitId",
                table: "RestockSourcingAllocations",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ProductionRunId",
                table: "RestockSourcingAllocations",
                column: "ProductionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseAdviceLineId",
                unique: true,
                filter: "[PurchaseAdviceLineId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseOrderLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseOrderLineId",
                unique: true,
                filter: "[PurchaseOrderLineId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ReleasedByStaffId",
                table: "RestockSourcingAllocations",
                column: "ReleasedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_DecisionType_Status",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "DecisionType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_SourceDocumentType_SourceDocumentId_SourceDocumentLineId",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId" },
                unique: true,
                filter: "[SourceDocumentType] IS NOT NULL AND [SourceDocumentId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_Status",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_BranchReceiptLines_Units_InventoryBaseUnitId",
                table: "BranchReceiptLines",
                column: "InventoryBaseUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchReceiptLines_Units_ProcurementUnitId",
                table: "BranchReceiptLines",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceLines_RestockSourcingAllocations_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines",
                column: "RestockSourcingAllocationId",
                principalTable: "RestockSourcingAllocations",
                principalColumn: "RestockSourcingAllocationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceLines_Units_ProcurementUnitId",
                table: "PurchaseAdviceLines",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderBatchLines_Units_ProcurementUnitId",
                table: "PurchaseOrderBatchLines",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLineAllocations_Units_ProcurementUnitId",
                table: "PurchaseOrderLineAllocations",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_PurchaseAdviceLines_PurchaseAdviceLineId",
                table: "PurchaseOrderLines",
                column: "PurchaseAdviceLineId",
                principalTable: "PurchaseAdviceLines",
                principalColumn: "PurchaseAdviceLineId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_Units_InventoryBaseUnitId",
                table: "PurchaseOrderLines",
                column: "InventoryBaseUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_Units_ProcurementUnitId",
                table: "PurchaseOrderLines",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderReceiptPostings_Units_InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "InventoryBaseUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderReceiptPostings_Units_ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_PurchaseOrders_MasterPurchaseOrderId",
                table: "PurchaseOrders",
                column: "MasterPurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestockRequests_Stores_CreatedForStoreId",
                table: "RestockRequests",
                column: "CreatedForStoreId",
                principalTable: "Stores",
                principalColumn: "StoreId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestockRequests_Units_ProcurementUnitId",
                table: "RestockRequests",
                column: "ProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_Units_InventoryBaseUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_Units_ProcurementUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_RestockSourcingAllocations_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_Units_ProcurementUnitId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderBatchLines_Units_ProcurementUnitId",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLineAllocations_Units_ProcurementUnitId",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_PurchaseAdviceLines_PurchaseAdviceLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Units_InventoryBaseUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Units_ProcurementUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderReceiptPostings_Units_InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderReceiptPostings_Units_ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_PurchaseOrders_MasterPurchaseOrderId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockRequests_Stores_CreatedForStoreId",
                table: "RestockRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockRequests_Units_ProcurementUnitId",
                table: "RestockRequests");

            migrationBuilder.DropTable(
                name: "RestockSourcingAllocations");

            migrationBuilder.DropIndex(
                name: "IX_RestockRequests_CreatedForStoreId",
                table: "RestockRequests");

            migrationBuilder.DropIndex(
                name: "IX_RestockRequests_ProcurementUnitId",
                table: "RestockRequests");

            migrationBuilder.DropIndex(
                name: "IX_RestockRequests_StoreId_SourceType_Status",
                table: "RestockRequests");

            migrationBuilder.DropIndex(
                name: "IX_RestockRequests_StoreId_SourcingStatus",
                table: "RestockRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_MasterPurchaseOrderId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderReceiptPostings_InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderReceiptPostings_ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_InventoryBaseUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_ProcurementUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_PurchaseAdviceLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLineAllocations_ProcurementUnitId",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderBatchLines_ProcurementUnitId",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceLines_ProcurementUnitId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceLines_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseAdviceLines_ProcurementRequestedPositive",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropIndex(
                name: "IX_BranchReceiptLines_InventoryBaseUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_BranchReceiptLines_ProcurementUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "CreatedForStoreId",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "ForecastEvidence",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "NeedByDate",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "RequestedProcurementQuantity",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "SourceReferenceId",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "SourcingDecision",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "SourcingStatus",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "TargetStockProcurementQuantity",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "MasterPurchaseOrderId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "InventoryPostingBaseQuantity",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "ProcurementToInventoryFactor",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "RejectedProcurementQuantity",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "AcceptedPackQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ClosedProcurementQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "InventoryBaseUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "InventoryPostingBaseQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "OrderedPackQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "OrderedProcurementQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PackSizeProcurementQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ProcurementToInventoryFactor",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurchaseAdviceLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "AllocatedProcurementQuantity",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropColumn(
                name: "DemandCoveredProcurementQuantity",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropColumn(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropColumn(
                name: "DemandCoveredProcurementQuantity",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "RoundingSurplusProcurementQuantity",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "TotalProcurementQuantity",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "AcceptedProcurementQuantity",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "AllocatedToPoProcurementQuantity",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "ClosedProcurementQuantity",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "RequestedProcurementQuantity",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "RestockSourcingAllocationId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "AllowsLoosePurchase",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "AcceptedPackQuantity",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "AcceptedProcurementQuantity",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "InventoryBaseUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "InventoryPostingBaseQuantity",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "ProcurementToInventoryFactor",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "ProcurementUnitId",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "ReceivedPackQuantity",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "ReceivedProcurementQuantity",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "RejectedProcurementQuantity",
                table: "BranchReceiptLines");
        }
    }
}
