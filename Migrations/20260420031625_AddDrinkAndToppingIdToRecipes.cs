using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddDrinkAndToppingIdToRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Drinks_DrinkId",
                table: "Recipes");

            migrationBuilder.AddColumn<int>(
                name: "ToppingId",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 1,
                columns: new[] { "DrinkId", "ToppingId" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 2,
                columns: new[] { "DrinkId", "ToppingId" },
                values: new object[] { 2, null });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 3,
                columns: new[] { "DrinkId", "ToppingId" },
                values: new object[] { 3, null });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 4,
                columns: new[] { "DrinkId", "ToppingId" },
                values: new object[] { 4, null });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 5,
                column: "ToppingId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 6,
                column: "ToppingId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 20, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1356), new DateTime(2026, 4, 13, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1337) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 5, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1362), new DateTime(2026, 4, 19, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1360) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 19, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1367), new DateTime(2026, 3, 21, 10, 16, 24, 330, DateTimeKind.Local).AddTicks(1365) });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ToppingId",
                table: "Recipes",
                column: "ToppingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Drinks_DrinkId",
                table: "Recipes",
                column: "DrinkId",
                principalTable: "Drinks",
                principalColumn: "DrinkId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Toppings_ToppingId",
                table: "Recipes",
                column: "ToppingId",
                principalTable: "Toppings",
                principalColumn: "ToppingId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Drinks_DrinkId",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Toppings_ToppingId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_ToppingId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ToppingId",
                table: "Recipes");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 1,
                column: "DrinkId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 2,
                column: "DrinkId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 3,
                column: "DrinkId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 4,
                column: "DrinkId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(94), new DateTime(2026, 4, 12, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(86) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(98), new DateTime(2026, 4, 18, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(97) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(100), new DateTime(2026, 3, 20, 0, 33, 50, 76, DateTimeKind.Local).AddTicks(99) });

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Drinks_DrinkId",
                table: "Recipes",
                column: "DrinkId",
                principalTable: "Drinks",
                principalColumn: "DrinkId");
        }
    }
}
