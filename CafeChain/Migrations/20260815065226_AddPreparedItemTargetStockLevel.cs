using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedItemTargetStockLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TargetStockLevel",
                table: "StoreInventories",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventories_TargetStock_AtLeastMin",
                table: "StoreInventories",
                sql: "[TargetStockLevel] IS NULL OR [MinStockLevel] IS NULL OR [TargetStockLevel] >= [MinStockLevel]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventories_TargetStock_NonNegative",
                table: "StoreInventories",
                sql: "[TargetStockLevel] IS NULL OR [TargetStockLevel] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventories_TargetStock_AtLeastMin",
                table: "StoreInventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventories_TargetStock_NonNegative",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "TargetStockLevel",
                table: "StoreInventories");
        }
    }
}
