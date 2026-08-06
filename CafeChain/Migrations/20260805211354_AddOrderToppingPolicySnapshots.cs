using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderToppingPolicySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostTreatmentSnapshot",
                table: "OrderToppings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ADD_TOPPING_RECIPE_COST");

            migrationBuilder.AddColumn<string>(
                name: "PriceTreatmentSnapshot",
                table: "OrderToppings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ADD_TOPPING_PRICE");

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityPerDrinkSnapshot",
                table: "OrderToppings",
                type: "decimal(18,5)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderToppings_CostTreatmentSnapshot",
                table: "OrderToppings",
                sql: "[CostTreatmentSnapshot] IN ('INCLUDED_IN_DRINK_RECIPE','ADD_TOPPING_RECIPE_COST')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderToppings_PriceTreatmentSnapshot",
                table: "OrderToppings",
                sql: "[PriceTreatmentSnapshot] IN ('INCLUDED_IN_BASE_PRICE','ADD_TOPPING_PRICE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderToppings_QuantityPerDrinkSnapshot",
                table: "OrderToppings",
                sql: "[QuantityPerDrinkSnapshot] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderToppings_CostTreatmentSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderToppings_PriceTreatmentSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderToppings_QuantityPerDrinkSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropColumn(
                name: "CostTreatmentSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropColumn(
                name: "PriceTreatmentSnapshot",
                table: "OrderToppings");

            migrationBuilder.DropColumn(
                name: "QuantityPerDrinkSnapshot",
                table: "OrderToppings");
        }
    }
}
