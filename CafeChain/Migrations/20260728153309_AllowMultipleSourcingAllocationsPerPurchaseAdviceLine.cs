using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleSourcingAllocationsPerPurchaseAdviceLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseAdviceLineId",
                filter: "[PurchaseAdviceLineId] IS NOT NULL AND [Status] = 'ACTIVE'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseAdviceLineId",
                unique: true,
                filter: "[PurchaseAdviceLineId] IS NOT NULL AND [Status] = 'ACTIVE'");
        }
    }
}
