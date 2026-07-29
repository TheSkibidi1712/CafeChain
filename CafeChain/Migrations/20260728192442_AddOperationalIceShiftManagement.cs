using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalIceShiftManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IcePolicies",
                columns: table => new
                {
                    IcePolicyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    DisplayUnitId = table.Column<int>(type: "int", nullable: false),
                    SuggestedDailyQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SuggestedShiftQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AllowSupplementalIssue = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AllowSameDayCarryOver = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireVarianceApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    VarianceApprovalQuantityThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VarianceApprovalPercentThreshold = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcePolicies", x => x.IcePolicyId);
                    table.CheckConstraint("CK_IcePolicies_SuggestedQuantity", "[SuggestedDailyQuantity] >= 0 AND [SuggestedShiftQuantity] >= 0");
                    table.CheckConstraint("CK_IcePolicies_VarianceThreshold", "[VarianceApprovalQuantityThreshold] >= 0 AND [VarianceApprovalPercentThreshold] >= 0");
                    table.ForeignKey(
                        name: "FK_IcePolicies_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Staffs_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Units_DisplayUnitId",
                        column: x => x.DisplayUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalShifts",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftLeadId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    OpenedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShifts", x => x.OperationalShiftId);
                    table.CheckConstraint("CK_OperationalShifts_Status", "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
                    table.CheckConstraint("CK_OperationalShifts_TimeRange", "[EndAtUtc] > [StartAtUtc]");
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_ClosedByStaffId",
                        column: x => x.ClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_OpenedByStaffId",
                        column: x => x.OpenedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_ShiftLeadId",
                        column: x => x.ShiftLeadId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceAllocations",
                columns: table => new
                {
                    IceAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    IcePolicyId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    OpeningCarryQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    InitialIssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SupplementalIssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingCarryQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TheoreticalUsageQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualUsageQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VarianceQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ReservedOutstandingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CostSnapshotStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Missing"),
                    ReservationReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    ReconciliationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReturnReceivedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    OpenedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceAllocations", x => x.IceAllocationId);
                    table.CheckConstraint("CK_IceAllocations_CostStatus", "[CostSnapshotStatus] IN ('Available','Missing')");
                    table.CheckConstraint("CK_IceAllocations_NonNegativeQuantities", "[OpeningCarryQuantity] >= 0 AND [InitialIssuedQuantity] >= 0 AND [SupplementalIssuedQuantity] >= 0 AND [ReturnedQuantity] >= 0 AND [ClosingCarryQuantity] >= 0 AND [TheoreticalUsageQuantity] >= 0 AND [ReservedOutstandingQuantity] >= 0");
                    table.CheckConstraint("CK_IceAllocations_ReturnAudit", "([ReturnedQuantity] = 0) OR ([ReturnedByStaffId] IS NOT NULL AND [ReturnReceivedByStaffId] IS NOT NULL AND [ReturnedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([ReturnCondition]))) > 0)");
                    table.CheckConstraint("CK_IceAllocations_Status", "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceAllocations_IcePolicies_IcePolicyId",
                        column: x => x.IcePolicyId,
                        principalTable: "IcePolicies",
                        principalColumn: "IcePolicyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ClosedByStaffId",
                        column: x => x.ClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_OpenedByStaffId",
                        column: x => x.OpenedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ReturnReceivedByStaffId",
                        column: x => x.ReturnReceivedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ReturnedByStaffId",
                        column: x => x.ReturnedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalShiftWorkShifts",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    WorkShiftId = table.Column<int>(type: "int", nullable: false),
                    LinkedByStaffId = table.Column<int>(type: "int", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShiftWorkShifts", x => new { x.OperationalShiftId, x.WorkShiftId });
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_Staffs_LinkedByStaffId",
                        column: x => x.LinkedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceCarryOvers",
                columns: table => new
                {
                    IceCarryOverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FromOperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    ToOperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    FromIceAllocationId = table.Column<int>(type: "int", nullable: false),
                    ToIceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    HandedOverByStaffId = table.Column<int>(type: "int", nullable: false),
                    ReceivedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceCarryOvers", x => x.IceCarryOverId);
                    table.CheckConstraint("CK_IceCarryOvers_DifferentShifts", "[FromOperationalShiftId] <> [ToOperationalShiftId]");
                    table.CheckConstraint("CK_IceCarryOvers_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceCarryOvers_Status", "[Status] IN ('Pending','Confirmed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_IceAllocations_FromIceAllocationId",
                        column: x => x.FromIceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_IceAllocations_ToIceAllocationId",
                        column: x => x.ToIceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_OperationalShifts_FromOperationalShiftId",
                        column: x => x.FromOperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_OperationalShifts_ToOperationalShiftId",
                        column: x => x.ToOperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_Staffs_HandedOverByStaffId",
                        column: x => x.HandedOverByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_Staffs_ReceivedByStaffId",
                        column: x => x.ReceivedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceInventoryPostings",
                columns: table => new
                {
                    IceInventoryPostingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    PostingType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceInventoryPostings", x => x.IceInventoryPostingId);
                    table.CheckConstraint("CK_IceInventoryPostings_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceInventoryPostings_Type", "[PostingType] IN ('VarianceOut')");
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_IceAllocations_IceAllocationId",
                        column: x => x.IceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceSupplementalIssues",
                columns: table => new
                {
                    IceSupplementalIssueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReservationApplied = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceSupplementalIssues", x => x.IceSupplementalIssueId);
                    table.CheckConstraint("CK_IceSupplementalIssues_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceSupplementalIssues_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_IceAllocations_IceAllocationId",
                        column: x => x.IceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_RejectedByStaffId",
                        column: x => x.RejectedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PermissionGroups",
                columns: new[] { "PermissionGroupId", "Active", "Code", "DisplayOrder", "Name" },
                values: new object[] { 20, true, "OPERATIONAL_ICE", 20, "Quản lý đá vận hành" });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "Action", "Active", "Code", "CreatedAt", "Description", "Name", "PermissionGroupId" },
                values: new object[,]
                {
                    { 200, "View", true, "OperationalIce.View", new DateTime(2026, 7, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), "Xem ca vận hành, phân bổ và đối soát đá", "Xem quản lý đá vận hành", 20 },
                    { 201, "Manage", true, "OperationalIce.Manage", new DateTime(2026, 7, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), "Tạo ca, mở phân bổ, cấp bổ sung và bàn giao đá", "Vận hành phân bổ đá", 20 },
                    { 202, "Approve", true, "OperationalIce.Approve", new DateTime(2026, 7, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), "Duyệt cấp bổ sung và chênh lệch đá cuối ca", "Duyệt đối soát đá", 20 },
                    { 203, "Policy", true, "OperationalIce.Policy", new DateTime(2026, 7, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cấu hình định mức và ngưỡng đối soát đá theo cửa hàng", "Cấu hình chính sách đá", 20 }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 200, 1 },
                    { 201, 1 },
                    { 202, 1 },
                    { 203, 1 },
                    { 200, 2 },
                    { 200, 3 },
                    { 201, 3 },
                    { 202, 3 },
                    { 203, 3 },
                    { 200, 4 },
                    { 200, 5 },
                    { 201, 5 },
                    { 202, 5 },
                    { 203, 5 },
                    { 200, 6 },
                    { 201, 6 },
                    { 202, 6 },
                    { 203, 6 },
                    { 200, 8 },
                    { 201, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ClosedByStaffId",
                table: "IceAllocations",
                column: "ClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_CreatedByStaffId",
                table: "IceAllocations",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_IcePolicyId",
                table: "IceAllocations",
                column: "IcePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_IngredientId",
                table: "IceAllocations",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_OpenedByStaffId",
                table: "IceAllocations",
                column: "OpenedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_OperationalShiftId_IngredientId",
                table: "IceAllocations",
                columns: new[] { "OperationalShiftId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_PublicId",
                table: "IceAllocations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReservationReference",
                table: "IceAllocations",
                column: "ReservationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReturnedByStaffId",
                table: "IceAllocations",
                column: "ReturnedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReturnReceivedByStaffId",
                table: "IceAllocations",
                column: "ReturnReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_StoreInventoryId_Status",
                table: "IceAllocations",
                columns: new[] { "StoreInventoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_FromIceAllocationId_ToIceAllocationId",
                table: "IceCarryOvers",
                columns: new[] { "FromIceAllocationId", "ToIceAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_FromOperationalShiftId",
                table: "IceCarryOvers",
                column: "FromOperationalShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_HandedOverByStaffId",
                table: "IceCarryOvers",
                column: "HandedOverByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_PublicId",
                table: "IceCarryOvers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ReceivedByStaffId",
                table: "IceCarryOvers",
                column: "ReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ToIceAllocationId",
                table: "IceCarryOvers",
                column: "ToIceAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ToOperationalShiftId",
                table: "IceCarryOvers",
                column: "ToOperationalShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_ApprovedByStaffId",
                table: "IceInventoryPostings",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_IceAllocationId_Revision_PostingType",
                table: "IceInventoryPostings",
                columns: new[] { "IceAllocationId", "Revision", "PostingType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_IdempotencyKey",
                table: "IceInventoryPostings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_InventoryTransactionId",
                table: "IceInventoryPostings",
                column: "InventoryTransactionId",
                unique: true,
                filter: "[InventoryTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_DisplayUnitId",
                table: "IcePolicies",
                column: "DisplayUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_IngredientId",
                table: "IcePolicies",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_StoreId",
                table: "IcePolicies",
                column: "StoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_StoreId_IngredientId",
                table: "IcePolicies",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_UpdatedByStaffId",
                table: "IcePolicies",
                column: "UpdatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_ApprovedByStaffId",
                table: "IceSupplementalIssues",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_IceAllocationId_Status",
                table: "IceSupplementalIssues",
                columns: new[] { "IceAllocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_PublicId",
                table: "IceSupplementalIssues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_RejectedByStaffId",
                table: "IceSupplementalIssues",
                column: "RejectedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_RequestedByStaffId",
                table: "IceSupplementalIssues",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_ClosedByStaffId",
                table: "OperationalShifts",
                column: "ClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_CreatedByStaffId",
                table: "OperationalShifts",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_OpenedByStaffId",
                table: "OperationalShifts",
                column: "OpenedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_ShiftLeadId",
                table: "OperationalShifts",
                column: "ShiftLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Status",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftWorkShifts_LinkedByStaffId",
                table: "OperationalShiftWorkShifts",
                column: "LinkedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftWorkShifts_WorkShiftId",
                table: "OperationalShiftWorkShifts",
                column: "WorkShiftId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IceCarryOvers");

            migrationBuilder.DropTable(
                name: "IceInventoryPostings");

            migrationBuilder.DropTable(
                name: "IceSupplementalIssues");

            migrationBuilder.DropTable(
                name: "OperationalShiftWorkShifts");

            migrationBuilder.DropTable(
                name: "IceAllocations");

            migrationBuilder.DropTable(
                name: "IcePolicies");

            migrationBuilder.DropTable(
                name: "OperationalShifts");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 201, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 202, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 203, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 201, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 202, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 203, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 201, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 202, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 203, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 201, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 202, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 203, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 200, 8 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 201, 8 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 200);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 201);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 202);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 203);

            migrationBuilder.DeleteData(
                table: "PermissionGroups",
                keyColumn: "PermissionGroupId",
                keyValue: 20);
        }
    }
}
