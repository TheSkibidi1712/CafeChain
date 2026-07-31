using CafeChain.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <summary>
    /// Permission.Code is the RBAC business key. A permission group may expose
    /// more than one permission with the same action name (for example the
    /// RESTOCK group has separate View permissions for requests and reorder
    /// suggestions), so the supporting group/action index must not be unique.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260730220234_PermissionCodeBusinessKey")]
    public partial class PermissionCodeBusinessKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionGroupId_Action",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionGroupId_Action",
                table: "Permissions",
                columns: new[] { "PermissionGroupId", "Action" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionGroupId_Action",
                table: "Permissions");

            migrationBuilder.Sql(
                """
                UPDATE dbo.Permissions
                SET Action = N'ReorderSuggestionView'
                WHERE Code = N'ReorderSuggestion.View';

                UPDATE dbo.Permissions
                SET Action = N'ConsolidationView'
                WHERE Code = N'PurchaseAdviceConsolidation.View';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionGroupId_Action",
                table: "Permissions",
                columns: new[] { "PermissionGroupId", "Action" },
                unique: true);
        }
    }
}
