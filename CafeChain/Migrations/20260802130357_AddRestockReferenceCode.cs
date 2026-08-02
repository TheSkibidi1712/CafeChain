using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddRestockReferenceCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                table: "RestockRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [RestockRequests]
                SET [ReferenceCode] = CONCAT(
                    'RR-LEGACY-S',
                    [StoreId],
                    '-',
                    CONVERT(char(8), [CreatedAt], 112),
                    '-',
                    RIGHT(CONCAT('0000000000', [RestockRequestId]), 10))
                WHERE [ReferenceCode] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceCode",
                table: "RestockRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_RestockRequests_ReferenceCode",
                table: "RestockRequests",
                column: "ReferenceCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RestockRequests_ReferenceCode",
                table: "RestockRequests");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                table: "RestockRequests");
        }
    }
}
