using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class BOM_Versioning_Inventory_Shift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "Recipes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentVersionId",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Recipes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<int>(
                name: "WorkShiftId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceOrderId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkShifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedEndingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ActualEndingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShifts", x => x.ShiftId);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Staffs_UserId",
                        column: x => x.UserId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 1,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 2,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 3,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 4,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 5,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "RecipeId",
                keyValue: 6,
                columns: new[] { "EffectiveDate", "ParentVersionId", "Status" },
                values: new object[] { null, null, "Active" });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 22, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3005), new DateTime(2026, 4, 15, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(2978) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 7, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3009), new DateTime(2026, 4, 21, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3009) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 21, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3012), new DateTime(2026, 3, 23, 18, 8, 52, 291, DateTimeKind.Local).AddTicks(3011) });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ParentVersionId",
                table: "Recipes",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WorkShiftId",
                table: "Orders",
                column: "WorkShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceOrderId",
                table: "InventoryTransactions",
                column: "ReferenceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_StoreId",
                table: "WorkShifts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_UserId",
                table: "WorkShifts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Orders_ReferenceOrderId",
                table: "InventoryTransactions",
                column: "ReferenceOrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_WorkShifts_WorkShiftId",
                table: "Orders",
                column: "WorkShiftId",
                principalTable: "WorkShifts",
                principalColumn: "ShiftId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipe_ParentVersion",
                table: "Recipes",
                column: "ParentVersionId",
                principalTable: "Recipes",
                principalColumn: "RecipeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Orders_ReferenceOrderId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_WorkShifts_WorkShiftId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipe_ParentVersion",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "WorkShifts");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_ParentVersionId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Orders_WorkShiftId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ReferenceOrderId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ParentVersionId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WorkShiftId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferenceOrderId",
                table: "InventoryTransactions");

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
        }
    }
}
