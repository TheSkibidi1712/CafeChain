using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AllowNormalPoPurchaseAdviceFulfillmentTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrderLineAllocationId",
                table: "PurchaseAdviceFulfillmentPostings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "BranchReceiptLineId", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NOT NULL AND [PostingType] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "BranchReceiptLineId", "PurchaseOrderLineId", "PostingType" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NULL AND [PostingType] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "CloseOperationKey", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NOT NULL AND [PostingType] = 'CLOSED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "CloseOperationKey", "PurchaseOrderLineId", "PostingType" },
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NULL AND [PostingType] = 'CLOSED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM PurchaseAdviceFulfillmentPostings WHERE PurchaseOrderLineAllocationId IS NULL) " +
                "THROW 51000, 'Không thể rollback khi còn fulfillment posting của đơn đặt hàng thường.', 1;");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrderLineAllocationId",
                table: "PurchaseAdviceFulfillmentPostings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "BranchReceiptLineId", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL AND [PostingType] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "CloseOperationKey", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL AND [PostingType] = 'CLOSED'");
        }
    }
}
