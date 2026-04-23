using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddBOMAndInventoryUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventories_NonNegativeQty",
                table: "StoreInventories");

            migrationBuilder.AlterColumn<int>(
                name: "IngredientId",
                table: "StoreInventories",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "RecipeId",
                table: "StoreInventories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedCogs",
                table: "Drinks",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 1,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 2,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 3,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 4,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 5,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Drinks",
                keyColumn: "DrinkId",
                keyValue: 6,
                column: "CalculatedCogs",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "StoreInventories",
                keyColumn: "StoreInventoryId",
                keyValue: 1,
                column: "RecipeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "StoreInventories",
                keyColumn: "StoreInventoryId",
                keyValue: 2,
                column: "RecipeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "StoreInventories",
                keyColumn: "StoreInventoryId",
                keyValue: 3,
                column: "RecipeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "StoreInventories",
                keyColumn: "StoreInventoryId",
                keyValue: 4,
                column: "RecipeId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2658), new DateTime(2026, 4, 15, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2626) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2662), new DateTime(2026, 4, 21, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2662) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2668), new DateTime(2026, 3, 23, 13, 36, 39, 866, DateTimeKind.Local).AddTicks(2667) });

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_RecipeId",
                table: "StoreInventories",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Recipe",
                table: "StoreInventories",
                columns: new[] { "StoreId", "RecipeId" },
                unique: true,
                filter: "[RecipeId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventories_XOR_Item",
                table: "StoreInventories",
                sql: "([IngredientId] IS NOT NULL AND [RecipeId] IS NULL) OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Recipes_RecipeId",
                table: "StoreInventories",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "RecipeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Recipes_RecipeId",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StoreInventories_RecipeId",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "UX_Store_Recipe",
                table: "StoreInventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreInventories_XOR_Item",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "CalculatedCogs",
                table: "Drinks");

            migrationBuilder.AlterColumn<int>(
                name: "IngredientId",
                table: "StoreInventories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3539), new DateTime(2026, 4, 15, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3507) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3547), new DateTime(2026, 4, 21, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3546) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3549), new DateTime(2026, 3, 23, 11, 16, 50, 749, DateTimeKind.Local).AddTicks(3548) });

            migrationBuilder.CreateIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreInventories_NonNegativeQty",
                table: "StoreInventories",
                sql: "[AvailableQty] >= 0 AND [ReservedQty] >= 0");
        }
    }
}
