using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddLooseProcurementContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderLineAllocations_PackagePositive",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PackagePositive",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PriceNonNegative",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BranchReceiptLines_Quantities",
                table: "BranchReceiptLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BranchReceiptLines_RejectionReason",
                table: "BranchReceiptLines");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "PurchaseOrderReceiptPostings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.AlterColumn<int>(
                name: "PackageUnitIdSnapshot",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageQuantitySnapshot",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackagePriceSnapshot",
                table: "PurchaseOrderLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageCount",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedPackageCount",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "PurchaseOrderLines",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPricePerPackage",
                table: "PurchaseOrderLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPricePerProcurementUnit",
                table: "PurchaseOrderLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AllocatedPackageQuantity",
                table: "PurchaseOrderLineAllocations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "PurchaseOrderLineAllocations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPackageCount",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "PackageUnitId",
                table: "PurchaseOrderBatchLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageQuantitySnapshot",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackagePriceSnapshot",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedPackageCount",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "PurchaseOrderBatchLines",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPricePerPackage",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPricePerProcurementUnit",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "PurchaseAdviceLines",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentProcurementUnitPrice",
                table: "IngredientSuppliers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LooseProcurementUnitId",
                table: "IngredientSuppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseMode",
                table: "BranchReceiptLines",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Packaged");

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 1,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 2,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 3,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 4,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 5,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 6,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 7,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 8,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 9,
                columns: new[] { "CurrentProcurementUnitPrice", "LooseProcurementUnitId" },
                values: new object[] { null, null });

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [PurchaseOrderLines]
                    WHERE [PackageCount] <= 0 OR [PackageCount] <> FLOOR([PackageCount])
                )
                    THROW 51001, 'Cannot backfill Packaged purchase-order lines because a legacy package count is non-positive or fractional.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [PurchaseOrderBatchLines]
                    WHERE [TotalPackageCount] <= 0 OR [TotalPackageCount] <> FLOOR([TotalPackageCount])
                )
                    THROW 51002, 'Cannot backfill Packaged batch lines because a legacy package count is non-positive or fractional.', 1;

                UPDATE [PurchaseOrderLines]
                SET [PurchaseMode] = N'Packaged',
                    [OrderedPackageCount] = [PackageCount],
                    [UnitPricePerPackage] = [PackagePriceSnapshot],
                    [UnitPricePerProcurementUnit] = NULL;

                UPDATE [PurchaseOrderBatchLines]
                SET [PurchaseMode] = N'Packaged',
                    [OrderedPackageCount] = [TotalPackageCount],
                    [UnitPricePerPackage] = [PackagePriceSnapshot],
                    [UnitPricePerProcurementUnit] = NULL;

                UPDATE [PurchaseOrderLineAllocations]
                SET [PurchaseMode] = N'Packaged';

                UPDATE [PurchaseAdviceLines]
                SET [PurchaseMode] = N'Packaged';

                UPDATE [PurchaseOrderReceiptPostings]
                SET [PurchaseMode] = N'Packaged';

                UPDATE [BranchReceiptLines]
                SET [PurchaseMode] = N'Packaged';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderLines_PurchaseModeAuthority",
                table: "PurchaseOrderLines",
                sql: "([PurchaseMode] = 'Packaged' AND [OrderedPackageCount] IS NOT NULL AND [OrderedPackageCount] > 0 AND [OrderedPackageCount] = FLOOR([OrderedPackageCount]) AND [UnitPricePerPackage] IS NOT NULL AND [UnitPricePerPackage] >= 0 AND [UnitPricePerProcurementUnit] IS NULL AND ([PackSizeProcurementQuantity] IS NULL OR [PackSizeProcurementQuantity] > 0)) OR ([PurchaseMode] = 'Loose' AND [OrderedPackageCount] IS NULL AND [OrderedProcurementQuantity] IS NOT NULL AND [OrderedProcurementQuantity] > 0 AND [ProcurementUnitId] IS NOT NULL AND [UnitPricePerProcurementUnit] IS NOT NULL AND [UnitPricePerProcurementUnit] >= 0 AND [UnitPricePerPackage] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderLineAllocations_PurchaseModeAuthority",
                table: "PurchaseOrderLineAllocations",
                sql: "([PurchaseMode] = 'Packaged' AND [AllocatedPackageQuantity] IS NOT NULL AND [AllocatedPackageQuantity] > 0) OR ([PurchaseMode] = 'Loose' AND [AllocatedPackageQuantity] IS NULL AND [AllocatedProcurementQuantity] IS NOT NULL AND [AllocatedProcurementQuantity] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PriceNonNegative",
                table: "PurchaseOrderBatchLines",
                sql: "([PackagePriceSnapshot] IS NULL OR [PackagePriceSnapshot] >= 0) AND [LineTotal] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PurchaseModeAuthority",
                table: "PurchaseOrderBatchLines",
                sql: "([PurchaseMode] = 'Packaged' AND [PackageQuantitySnapshot] IS NOT NULL AND [PackageQuantitySnapshot] > 0 AND [OrderedPackageCount] IS NOT NULL AND [OrderedPackageCount] > 0 AND [OrderedPackageCount] = FLOOR([OrderedPackageCount]) AND [UnitPricePerPackage] IS NOT NULL AND [UnitPricePerPackage] >= 0 AND [UnitPricePerProcurementUnit] IS NULL) OR ([PurchaseMode] = 'Loose' AND [OrderedPackageCount] IS NULL AND [TotalProcurementQuantity] IS NOT NULL AND [TotalProcurementQuantity] > 0 AND [ProcurementUnitId] IS NOT NULL AND [UnitPricePerProcurementUnit] IS NOT NULL AND [UnitPricePerProcurementUnit] >= 0 AND [UnitPricePerPackage] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_LooseProcurementUnitId",
                table: "IngredientSuppliers",
                column: "LooseProcurementUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IngredientSupplier_LoosePurchase",
                table: "IngredientSuppliers",
                sql: "[AllowsLoosePurchase] = 0 OR ([CurrentProcurementUnitPrice] IS NOT NULL AND [CurrentProcurementUnitPrice] > 0 AND [LooseProcurementUnitId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BranchReceiptLines_Quantities",
                table: "BranchReceiptLines",
                sql: "[InputQuantity] > 0 AND [ReceivedBaseQuantity] >= 0 AND [RejectedBaseQuantity] >= 0 AND (([ReceivedProcurementQuantity] IS NOT NULL AND [ReceivedProcurementQuantity] > 0 AND [AcceptedProcurementQuantity] IS NOT NULL AND [AcceptedProcurementQuantity] >= 0 AND [RejectedProcurementQuantity] IS NOT NULL AND [RejectedProcurementQuantity] >= 0) OR ([ReceivedBaseQuantity] + [RejectedBaseQuantity]) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BranchReceiptLines_RejectionReason",
                table: "BranchReceiptLines",
                sql: "([RejectedBaseQuantity] = 0 AND ([RejectedProcurementQuantity] IS NULL OR [RejectedProcurementQuantity] = 0)) OR (LEN(LTRIM(RTRIM([RejectionReason]))) > 0 AND LEN(LTRIM(RTRIM([RejectionIssueType]))) > 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientSuppliers_Units_LooseProcurementUnitId",
                table: "IngredientSuppliers",
                column: "LooseProcurementUnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [PurchaseOrderLines] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [PurchaseOrderBatchLines] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [PurchaseOrderLineAllocations] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [PurchaseAdviceLines] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [PurchaseOrderReceiptPostings] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [BranchReceiptLines] WHERE [PurchaseMode] = N'Loose')
                    OR EXISTS (SELECT 1 FROM [IngredientSuppliers] WHERE [AllowsLoosePurchase] = 1)
                    THROW 51003, 'Cannot roll back AddLooseProcurementContract while loose procurement data exists.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientSuppliers_Units_LooseProcurementUnitId",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderLines_PurchaseModeAuthority",
                table: "PurchaseOrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderLineAllocations_PurchaseModeAuthority",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PriceNonNegative",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PurchaseModeAuthority",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropIndex(
                name: "IX_IngredientSuppliers_LooseProcurementUnitId",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IngredientSupplier_LoosePurchase",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BranchReceiptLines_Quantities",
                table: "BranchReceiptLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BranchReceiptLines_RejectionReason",
                table: "BranchReceiptLines");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropColumn(
                name: "OrderedPackageCount",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "UnitPricePerPackage",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "UnitPricePerProcurementUnit",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "PurchaseOrderLineAllocations");

            migrationBuilder.DropColumn(
                name: "OrderedPackageCount",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "UnitPricePerPackage",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "UnitPricePerProcurementUnit",
                table: "PurchaseOrderBatchLines");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropColumn(
                name: "CurrentProcurementUnitPrice",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "LooseProcurementUnitId",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "PurchaseMode",
                table: "BranchReceiptLines");

            migrationBuilder.AlterColumn<int>(
                name: "PackageUnitIdSnapshot",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageQuantitySnapshot",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackagePriceSnapshot",
                table: "PurchaseOrderLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageCount",
                table: "PurchaseOrderLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AllocatedPackageQuantity",
                table: "PurchaseOrderLineAllocations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPackageCount",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PackageUnitId",
                table: "PurchaseOrderBatchLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackageQuantitySnapshot",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PackagePriceSnapshot",
                table: "PurchaseOrderBatchLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderLineAllocations_PackagePositive",
                table: "PurchaseOrderLineAllocations",
                sql: "[AllocatedPackageQuantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PackagePositive",
                table: "PurchaseOrderBatchLines",
                sql: "[PackageQuantitySnapshot] > 0 AND [TotalPackageCount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderBatchLines_PriceNonNegative",
                table: "PurchaseOrderBatchLines",
                sql: "[PackagePriceSnapshot] >= 0 AND [LineTotal] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BranchReceiptLines_Quantities",
                table: "BranchReceiptLines",
                sql: "[InputQuantity] > 0 AND [ReceivedBaseQuantity] >= 0 AND [RejectedBaseQuantity] >= 0 AND ([ReceivedBaseQuantity] + [RejectedBaseQuantity]) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BranchReceiptLines_RejectionReason",
                table: "BranchReceiptLines",
                sql: "[RejectedBaseQuantity] = 0 OR (LEN(LTRIM(RTRIM([RejectionReason]))) > 0 AND LEN(LTRIM(RTRIM([RejectionIssueType]))) > 0)");
        }
    }
}
