using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class FixInventoryDebtDocumentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDebts_InventoryDocuments_InventoryDocumentId1",
                table: "InventoryDebts");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDebts_InventoryDocumentId1",
                table: "InventoryDebts");

            migrationBuilder.DropColumn(
                name: "InventoryDocumentId1",
                table: "InventoryDebts");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryDocumentId1",
                table: "InventoryDebts",
                type: "int",
                nullable: false,
                defaultValue: 0);


            migrationBuilder.CreateIndex(
                name: "IX_InventoryDebts_InventoryDocumentId1",
                table: "InventoryDebts",
                column: "InventoryDocumentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDebts_InventoryDocuments_InventoryDocumentId1",
                table: "InventoryDebts",
                column: "InventoryDocumentId1",
                principalTable: "InventoryDocuments",
                principalColumn: "InventoryDocumentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
