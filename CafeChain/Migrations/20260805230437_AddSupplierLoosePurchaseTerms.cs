using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierLoosePurchaseTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngredientSuppliers_IngredientId",
                table: "IngredientSuppliers");

            migrationBuilder.AddColumn<decimal>(
                name: "LooseMinimumOrderQuantity",
                table: "IngredientSuppliers",
                type: "decimal(18,5)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoosePriceMode",
                table: "IngredientSuppliers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "INDEPENDENT");

            migrationBuilder.AddColumn<decimal>(
                name: "LooseQuantityStep",
                table: "IngredientSuppliers",
                type: "decimal(18,5)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 1,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 2,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 3,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 4,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 5,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 6,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 7,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 8,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.UpdateData(
                table: "IngredientSuppliers",
                keyColumn: "IngredientSupplierId",
                keyValue: 9,
                columns: new[] { "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseQuantityStep" },
                values: new object[] { null, "INDEPENDENT", null });

            migrationBuilder.CreateIndex(
                name: "UX_IngredientSuppliers_PrimaryByIngredient",
                table: "IngredientSuppliers",
                column: "IngredientId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [Active] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IngredientSupplier_LooseMinimumOrderQuantity",
                table: "IngredientSuppliers",
                sql: "[LooseMinimumOrderQuantity] IS NULL OR [LooseMinimumOrderQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IngredientSupplier_LoosePriceMode",
                table: "IngredientSuppliers",
                sql: "[LoosePriceMode] IN ('DERIVED', 'INDEPENDENT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IngredientSupplier_LooseQuantityStep",
                table: "IngredientSuppliers",
                sql: "[LooseQuantityStep] IS NULL OR [LooseQuantityStep] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_IngredientSuppliers_PrimaryByIngredient",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IngredientSupplier_LooseMinimumOrderQuantity",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IngredientSupplier_LoosePriceMode",
                table: "IngredientSuppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IngredientSupplier_LooseQuantityStep",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "LooseMinimumOrderQuantity",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "LoosePriceMode",
                table: "IngredientSuppliers");

            migrationBuilder.DropColumn(
                name: "LooseQuantityStep",
                table: "IngredientSuppliers");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_IngredientId",
                table: "IngredientSuppliers",
                column: "IngredientId");
        }
    }
}
