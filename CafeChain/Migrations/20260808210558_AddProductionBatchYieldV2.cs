using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBatchYieldV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT ProductionRunId
                    FROM dbo.RestockSourcingAllocations
                    WHERE ProductionRunId IS NOT NULL
                    GROUP BY ProductionRunId
                    HAVING COUNT_BIG(*) > 1)
                BEGIN
                    THROW 53620, N'PRODUCTION_V2_MIGRATION_REVIEW_REQUIRED: Có lệnh sản xuất đang liên kết nhiều phân bổ Restock. Chạy báo cáo dry-run trước khi migration.', 1;
                END
                """);

            migrationBuilder.DropIndex(
                name: "IX_RestockSourcingAllocations_ProductionRunId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionRuns_Status",
                table: "ProductionRuns");

            migrationBuilder.AddColumn<decimal>(
                name: "YieldVarianceTolerancePercent",
                table: "Recipes",
                type: "decimal(9,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualRecordedAtUtc",
                table: "ProductionRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualRecordedByStaffId",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractVersion",
                table: "ProductionRuns",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedOutputBase",
                table: "ProductionRuns",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedOutputPerBatchBase",
                table: "ProductionRuns",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputBaseUnitId",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedBatchCount",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAtUtc",
                table: "ProductionRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleasedByStaffId",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "ProductionRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartedByStaffId",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VarianceApprovedAtUtc",
                table: "ProductionRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VarianceApprovedByStaffId",
                table: "ProductionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VarianceReason",
                table: "ProductionRuns",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YieldVarianceTolerancePercent",
                table: "ProductionRuns",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryItemSourceCapabilities",
                columns: table => new
                {
                    InventoryItemSourceCapabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    CanProduce = table.Column<bool>(type: "bit", nullable: false),
                    CanPurchase = table.Column<bool>(type: "bit", nullable: false),
                    CanTransfer = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemSourceCapabilities", x => x.InventoryItemSourceCapabilityId);
                    table.CheckConstraint("CK_InventoryItemSourceCapabilities_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_InventoryItemSourceCapabilities_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventoryItemSourceCapabilities_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItemSourceCapabilities_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunInputActuals",
                columns: table => new
                {
                    ProductionRunInputActualId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    PlannedBaseQuantity = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ActualBaseQuantity = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunInputActuals", x => x.ProductionRunInputActualId);
                    table.CheckConstraint("CK_ProductionRunInputActuals_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_ProductionRunInputActuals_Quantities", "[PlannedBaseQuantity] >= 0 AND [ActualBaseQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunOutputs",
                columns: table => new
                {
                    ProductionRunOutputId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    ExpectedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ActualProducedBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    AcceptedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    RejectedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    VariancePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunOutputs", x => x.ProductionRunOutputId);
                    table.CheckConstraint("CK_ProductionRunOutputs_Quantities", "[ExpectedOutputBase] > 0 AND [ActualProducedBase] >= 0 AND [AcceptedOutputBase] >= 0 AND [RejectedOutputBase] >= 0 AND [AcceptedOutputBase] + [RejectedOutputBase] <= [ActualProducedBase]");
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_Staffs_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunTransitions",
                columns: table => new
                {
                    ProductionRunTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunTransitions", x => x.ProductionRunTransitionId);
                    table.ForeignKey(
                        name: "FK_ProductionRunTransitions_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreProductionCapabilities",
                columns: table => new
                {
                    StoreProductionCapabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProductionCapabilities", x => x.StoreProductionCapabilityId);
                    table.CheckConstraint("CK_StoreProductionCapabilities_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_StoreProductionCapabilities_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 1,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 2,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 3,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 4,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 5,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 6,
                column: "YieldVarianceTolerancePercent",
                value: null);

            migrationBuilder.CreateIndex(
                name: "UX_RestockSourcingAllocations_ProductionRun",
                table: "RestockSourcingAllocations",
                column: "ProductionRunId",
                unique: true,
                filter: "[ProductionRunId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipes_YieldVarianceTolerance",
                table: "Recipes",
                sql: "[YieldVarianceTolerancePercent] IS NULL OR ([YieldVarianceTolerancePercent] >= 0 AND [YieldVarianceTolerancePercent] <= 100)");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_OutputBaseUnitId",
                table: "ProductionRuns",
                column: "OutputBaseUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionRuns_Status",
                table: "ProductionRuns",
                sql: "[Status] IN (1, 2, 10, 11, 12, 13, 14, 15)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionRuns_V2BatchContract",
                table: "ProductionRuns",
                sql: "[ContractVersion] = 1 OR ([ContractVersion] = 2 AND [PlannedBatchCount] IS NOT NULL AND [PlannedBatchCount] > 0)");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItemSourceCapabilities_Ingredient",
                table: "InventoryItemSourceCapabilities",
                column: "IngredientId",
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItemSourceCapabilities_PreparedItem",
                table: "InventoryItemSourceCapabilities",
                column: "PreparedItemId",
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_BaseUnitId",
                table: "ProductionRunInputActuals",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ConfirmedByStaffId",
                table: "ProductionRunInputActuals",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_IngredientId",
                table: "ProductionRunInputActuals",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_PreparedItemId",
                table: "ProductionRunInputActuals",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ProductionRunId_IngredientId",
                table: "ProductionRunInputActuals",
                columns: new[] { "ProductionRunId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ProductionRunId_PreparedItemId",
                table: "ProductionRunInputActuals",
                columns: new[] { "ProductionRunId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_BaseUnitId",
                table: "ProductionRunOutputs",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_ProductionRunId",
                table: "ProductionRunOutputs",
                column: "ProductionRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_RecordedByStaffId",
                table: "ProductionRunOutputs",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunTransitions_ActorStaffId",
                table: "ProductionRunTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunTransitions_ProductionRunId_OccurredAtUtc",
                table: "ProductionRunTransitions",
                columns: new[] { "ProductionRunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductionCapabilities_IngredientId",
                table: "StoreProductionCapabilities",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductionCapabilities_PreparedItemId",
                table: "StoreProductionCapabilities",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "UX_StoreProductionCapabilities_Store_Ingredient",
                table: "StoreProductionCapabilities",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_StoreProductionCapabilities_Store_PreparedItem",
                table: "StoreProductionCapabilities",
                columns: new[] { "StoreId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRuns_Units_OutputBaseUnitId",
                table: "ProductionRuns",
                column: "OutputBaseUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DECLARE @ProductionGroupId int =
                    (SELECT TOP (1) PermissionGroupId FROM dbo.PermissionGroups WHERE Code = N'BOM');
                IF @ProductionGroupId IS NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM dbo.PermissionGroups WHERE PermissionGroupId = 25)
                    BEGIN
                        SET IDENTITY_INSERT dbo.PermissionGroups ON;
                        INSERT dbo.PermissionGroups(PermissionGroupId, Code, Name, DisplayOrder, Active)
                        VALUES (25, N'BOM', N'BOM và sản xuất', 26, 1);
                        SET IDENTITY_INSERT dbo.PermissionGroups OFF;
                        SET @ProductionGroupId = 25;
                    END
                    ELSE
                    BEGIN
                        INSERT dbo.PermissionGroups(Code, Name, DisplayOrder, Active)
                        VALUES (N'BOM', N'BOM và sản xuất', 26, 1);
                        SET @ProductionGroupId = CONVERT(int, SCOPE_IDENTITY());
                    END
                END
                ELSE
                BEGIN
                    UPDATE dbo.PermissionGroups
                    SET Name = N'BOM và sản xuất', DisplayOrder = 26, Active = 1
                    WHERE PermissionGroupId = @ProductionGroupId;
                END

                DECLARE @ProductionPermissions TABLE
                (
                    Code nvarchar(100) NOT NULL PRIMARY KEY,
                    Name nvarchar(200) NOT NULL,
                    Action nvarchar(100) NOT NULL,
                    Description nvarchar(500) NOT NULL
                );
                INSERT @ProductionPermissions(Code, Name, Action, Description) VALUES
                    (N'ProductionOrder.Plan', N'Lập kế hoạch sản xuất', N'Plan', N'Lập kế hoạch số mẻ sản xuất trong phạm vi cửa hàng'),
                    (N'ProductionOrder.Release', N'Phát hành lệnh sản xuất', N'Release', N'Phát hành lệnh đã lập kế hoạch để ca vận hành tiếp nhận'),
                    (N'ProductionOrder.Start', N'Bắt đầu lệnh sản xuất', N'Start', N'Bắt đầu thực hiện lệnh sản xuất đã phát hành'),
                    (N'ProductionOrder.RecordActual', N'Ghi nhận sản xuất thực tế', N'RecordActual', N'Xác nhận đầu vào và sản lượng thực tế của lệnh sản xuất'),
                    (N'ProductionOrder.AcceptOutput', N'Xác nhận đầu ra sản xuất', N'AcceptOutput', N'Tiêu thụ đầu vào FIFO và nhập sản lượng đạt vào tồn kho'),
                    (N'ProductionOrder.ApproveVariance', N'Duyệt chênh lệch sản xuất', N'ApproveVariance', N'Duyệt chênh lệch sản lượng vượt ngưỡng theo maker-checker'),
                    (N'ProductionOrder.Cancel', N'Hủy lệnh sản xuất', N'Cancel', N'Hủy lệnh sản xuất chưa bắt đầu và giữ lịch sử'),
                    (N'Restock.SelectProductionSource', N'Chọn nguồn sản xuất cho yêu cầu', N'SelectProductionSource', N'Chọn nguồn sản xuất khi resolver xác nhận item và cửa hàng đủ điều kiện');

                UPDATE p
                SET p.Name = s.Name,
                    p.Action = s.Action,
                    p.Description = s.Description,
                    p.Active = 1
                FROM dbo.Permissions p
                JOIN @ProductionPermissions s ON s.Code = p.Code;

                INSERT dbo.Permissions(PermissionGroupId, Code, Name, Action, Description, Active, CreatedAt)
                SELECT @ProductionGroupId, s.Code, s.Name, s.Action, s.Description, 1, SYSUTCDATETIME()
                FROM @ProductionPermissions s
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = s.Code);

                DECLARE @ProductionRoleMatrix TABLE(Code nvarchar(100), RoleName nvarchar(100), Allowed bit);
                INSERT @ProductionRoleMatrix(Code, RoleName, Allowed) VALUES
                    (N'ProductionOrder.Plan', N'Quản lý chi nhánh', 1),
                    (N'ProductionOrder.Release', N'Quản lý chi nhánh', 1),
                    (N'ProductionOrder.Start', N'Ca trưởng', 1),
                    (N'ProductionOrder.RecordActual', N'Ca trưởng', 1),
                    (N'ProductionOrder.AcceptOutput', N'Quản lý chi nhánh', 1),
                    (N'ProductionOrder.ApproveVariance', N'Chủ doanh nghiệp', 1),
                    (N'ProductionOrder.Cancel', N'Quản lý chi nhánh', 1),
                    (N'Restock.SelectProductionSource', N'Kế toán/kho', 1),
                    (N'ProductionOrder.Plan', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.Release', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.Start', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.RecordActual', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.AcceptOutput', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.ApproveVariance', N'Quản trị hệ thống', 1),
                    (N'ProductionOrder.Cancel', N'Quản trị hệ thống', 1),
                    (N'Restock.SelectProductionSource', N'Quản trị hệ thống', 1);

                INSERT dbo.RolePermissions(RoleId, PermissionId)
                SELECT r.RoleId, p.PermissionId
                FROM @ProductionRoleMatrix m
                JOIN dbo.Roles r ON r.Name = m.RoleName AND r.Active = 1
                JOIN dbo.Permissions p ON p.Code = m.Code AND p.Active = 1
                WHERE m.Allowed = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.RolePermissions rp
                      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rp
                FROM dbo.RolePermissions rp
                JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
                WHERE p.Code IN (
                    N'ProductionOrder.Plan', N'ProductionOrder.Release', N'ProductionOrder.Start',
                    N'ProductionOrder.RecordActual', N'ProductionOrder.AcceptOutput',
                    N'ProductionOrder.ApproveVariance', N'ProductionOrder.Cancel',
                    N'Restock.SelectProductionSource');
                DELETE FROM dbo.Permissions
                WHERE Code IN (
                    N'ProductionOrder.Plan', N'ProductionOrder.Release', N'ProductionOrder.Start',
                    N'ProductionOrder.RecordActual', N'ProductionOrder.AcceptOutput',
                    N'ProductionOrder.ApproveVariance', N'ProductionOrder.Cancel',
                    N'Restock.SelectProductionSource');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Units_OutputBaseUnitId",
                table: "ProductionRuns");

            migrationBuilder.DropTable(
                name: "InventoryItemSourceCapabilities");

            migrationBuilder.DropTable(
                name: "ProductionRunInputActuals");

            migrationBuilder.DropTable(
                name: "ProductionRunOutputs");

            migrationBuilder.DropTable(
                name: "ProductionRunTransitions");

            migrationBuilder.DropTable(
                name: "StoreProductionCapabilities");

            migrationBuilder.DropIndex(
                name: "UX_RestockSourcingAllocations_ProductionRun",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipes_YieldVarianceTolerance",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_ProductionRuns_OutputBaseUnitId",
                table: "ProductionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionRuns_Status",
                table: "ProductionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionRuns_V2BatchContract",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "YieldVarianceTolerancePercent",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ActualRecordedAtUtc",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ActualRecordedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ContractVersion",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ExpectedOutputBase",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ExpectedOutputPerBatchBase",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "OutputBaseUnitId",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "PlannedBatchCount",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ReleasedAtUtc",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "ReleasedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "StartedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "VarianceApprovedAtUtc",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "VarianceApprovedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "VarianceReason",
                table: "ProductionRuns");

            migrationBuilder.DropColumn(
                name: "YieldVarianceTolerancePercent",
                table: "ProductionRuns");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ProductionRunId",
                table: "RestockSourcingAllocations",
                column: "ProductionRunId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionRuns_Status",
                table: "ProductionRuns",
                sql: "[Status] IN (1, 2)");
        }
    }
}
