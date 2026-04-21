using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddBankPrimaryAndDependentRel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "StaffDependents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "StaffBanks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "StaffBanks",
                keyColumn: "StaffBankId",
                keyValue: 1,
                column: "IsPrimary",
                value: false);

            migrationBuilder.UpdateData(
                table: "StaffBanks",
                keyColumn: "StaffBankId",
                keyValue: 2,
                column: "IsPrimary",
                value: false);

            migrationBuilder.UpdateData(
                table: "StaffBanks",
                keyColumn: "StaffBankId",
                keyValue: 3,
                column: "IsPrimary",
                value: false);

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4409), new DateTime(2026, 4, 12, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4391) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4413), new DateTime(2026, 4, 18, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4412) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4415), new DateTime(2026, 3, 20, 22, 43, 56, 826, DateTimeKind.Local).AddTicks(4415) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Relationship",
                table: "StaffDependents");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "StaffBanks");

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 19, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2909), new DateTime(2026, 4, 12, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2881) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 4, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2914), new DateTime(2026, 4, 18, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2913) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 18, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2916), new DateTime(2026, 3, 20, 22, 40, 14, 332, DateTimeKind.Local).AddTicks(2916) });
        }
    }
}
