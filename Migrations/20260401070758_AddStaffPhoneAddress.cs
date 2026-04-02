using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPhoneAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffAddresses",
                columns: table => new
                {
                    StaffAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAddresses", x => x.StaffAddressId);
                    table.ForeignKey(
                        name: "FK_StaffAddresses_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffPhones",
                columns: table => new
                {
                    StaffPhoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPhones", x => x.StaffPhoneId);
                    table.ForeignKey(
                        name: "FK_StaffPhones_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "StaffAddresses",
                columns: new[] { "StaffAddressId", "Address", "IsDefault", "StaffId" },
                values: new object[,]
                {
                    { 1, "123 Đường Nguyễn Huệ, Q1, TP.HCM", true, 1 },
                    { 2, "456 Đường Lê Lợi, Q3, TP.HCM", true, 2 },
                    { 3, "789 Đường Trần Hưng Đạo, Q5, TP.HCM", true, 3 }
                });

            migrationBuilder.InsertData(
                table: "StaffPhones",
                columns: new[] { "StaffPhoneId", "IsDefault", "Phone", "StaffId" },
                values: new object[,]
                {
                    { 1, true, "0901000001", 1 },
                    { 2, true, "0901000002", 2 },
                    { 3, true, "0901000003", 3 },
                    { 4, true, "0901000004", 4 },
                    { 5, true, "0901000005", 5 },
                    { 6, true, "0901000006", 6 },
                    { 7, true, "0901000007", 7 },
                    { 8, true, "0901000008", 8 },
                    { 9, true, "0901000009", 9 },
                    { 10, true, "0901000010", 10 },
                    { 11, true, "0901000011", 11 },
                    { 12, true, "0901000012", 12 },
                    { 13, true, "0901000013", 13 },
                    { 14, true, "0901000014", 14 },
                    { 15, true, "0901000015", 15 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAddresses_StaffId",
                table: "StaffAddresses",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPhones_StaffId",
                table: "StaffPhones",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAddresses");

            migrationBuilder.DropTable(
                name: "StaffPhones");
        }
    }
}
