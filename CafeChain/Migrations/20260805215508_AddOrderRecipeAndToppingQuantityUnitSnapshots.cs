using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRecipeAndToppingQuantityUnitSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuantityUnitSnapshot",
                table: "OrderToppings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RECIPE_PORTION");

            migrationBuilder.AddColumn<int>(
                name: "RecipeIdSnapshot",
                table: "OrderToppings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipeIdSnapshot",
                table: "OrderDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnit",
                table: "DrinkSizeToppingPolicies",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RECIPE_PORTION");

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_RecipeIdSnapshot",
                table: "OrderToppings",
                column: "RecipeIdSnapshot");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderToppings_QuantityUnitSnapshot",
                table: "OrderToppings",
                sql: "[QuantityUnitSnapshot] = 'RECIPE_PORTION'");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_RecipeIdSnapshot",
                table: "OrderDetails",
                column: "RecipeIdSnapshot");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DrinkSizeToppingPolicies_QuantityUnit",
                table: "DrinkSizeToppingPolicies",
                sql: "[QuantityUnit] = 'RECIPE_PORTION'");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Recipes_RecipeIdSnapshot",
                table: "OrderDetails",
                column: "RecipeIdSnapshot",
                principalTable: "Recipes",
                principalColumn: "RecipeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderToppings_Recipes_RecipeIdSnapshot",
                table: "OrderToppings",
                column: "RecipeIdSnapshot",
                principalTable: "Recipes",
                principalColumn: "RecipeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Recipes_RecipeIdSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderToppings_Recipes_RecipeIdSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropIndex(
                name: "IX_OrderToppings_RecipeIdSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderToppings_QuantityUnitSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_RecipeIdSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DrinkSizeToppingPolicies_QuantityUnit",
                table: "DrinkSizeToppingPolicies");

            migrationBuilder.DropColumn(
                name: "QuantityUnitSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropColumn(
                name: "RecipeIdSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropColumn(
                name: "RecipeIdSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                table: "DrinkSizeToppingPolicies");
        }
    }
}
