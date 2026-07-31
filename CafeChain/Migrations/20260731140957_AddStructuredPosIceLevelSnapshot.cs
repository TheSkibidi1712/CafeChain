using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredPosIceLevelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedIceQuantityBaseUnit",
                table: "OrderDetails",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseIceQuantityBaseUnit",
                table: "OrderDetails",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IceIngredientId",
                table: "OrderDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IceLevelPercent",
                table: "OrderDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_IceIngredientId",
                table: "OrderDetails",
                column: "IceIngredientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_IceLevelPercent",
                table: "OrderDetails",
                sql: "[IceLevelPercent] IS NULL OR [IceLevelPercent] IN (0, 50, 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_IceSnapshot",
                table: "OrderDetails",
                sql: "([IceLevelPercent] IS NULL AND [IceIngredientId] IS NULL AND [BaseIceQuantityBaseUnit] IS NULL AND [AppliedIceQuantityBaseUnit] IS NULL) OR ([IceLevelPercent] IS NOT NULL AND [IceIngredientId] IS NOT NULL AND [BaseIceQuantityBaseUnit] IS NOT NULL AND [AppliedIceQuantityBaseUnit] IS NOT NULL AND [BaseIceQuantityBaseUnit] >= 0 AND [AppliedIceQuantityBaseUnit] >= 0 AND [AppliedIceQuantityBaseUnit] <= [BaseIceQuantityBaseUnit])");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Ingredients_IceIngredientId",
                table: "OrderDetails",
                column: "IceIngredientId",
                principalTable: "Ingredients",
                principalColumn: "IngredientId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Ingredients_IceIngredientId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_IceIngredientId",
                table: "OrderDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_IceLevelPercent",
                table: "OrderDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_IceSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "AppliedIceQuantityBaseUnit",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "BaseIceQuantityBaseUnit",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "IceIngredientId",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "IceLevelPercent",
                table: "OrderDetails");
        }
    }
}
