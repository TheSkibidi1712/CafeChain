using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class AddDistrictSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE CustomerAddresses SET WardId = NULL;");
            migrationBuilder.Sql("UPDATE Stores SET WardId = NULL;");
            migrationBuilder.Sql("DELETE FROM Wards;");
            migrationBuilder.Sql("DELETE FROM Provinces;");

            // --- BƯỚC KHẮC PHỤC LỖI THIẾU BẢNG BẰNG SQL NATIVE ---
            string sqlSchema = @"
                IF EXISTS(SELECT * FROM sys.foreign_keys WHERE name = 'FK_Wards_Provinces_ProvinceId')
                    ALTER TABLE [Wards] DROP CONSTRAINT [FK_Wards_Provinces_ProvinceId];
                IF EXISTS(SELECT * FROM sys.indexes WHERE name = 'IX_Wards_ProvinceId_Name' AND object_id = OBJECT_ID('Wards'))
                    DROP INDEX [IX_Wards_ProvinceId_Name] ON [Wards];
                IF EXISTS(SELECT * FROM sys.indexes WHERE name = 'IX_Wards_ProvinceId' AND object_id = OBJECT_ID('Wards'))
                    DROP INDEX [IX_Wards_ProvinceId] ON [Wards];
                IF EXISTS(SELECT * FROM sys.columns WHERE Name = N'ProvinceId' AND Object_ID = Object_ID(N'Wards'))
                    ALTER TABLE [Wards] DROP COLUMN [ProvinceId];

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Districts]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [Districts] (
                        [DistrictId] int NOT NULL IDENTITY,
                        [Name] nvarchar(150) NOT NULL,
                        [ProvinceId] int NULL,
                        CONSTRAINT [PK_Districts] PRIMARY KEY ([DistrictId]),
                        CONSTRAINT [FK_Districts_Provinces_ProvinceId] FOREIGN KEY ([ProvinceId]) REFERENCES [Provinces] ([ProvinceId]) ON DELETE CASCADE
                    );
                END

                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'DistrictId' AND Object_ID = Object_ID(N'Wards'))
                BEGIN
                    ALTER TABLE [Wards] ADD [DistrictId] int NULL;
                    ALTER TABLE [Wards] ADD CONSTRAINT [FK_Wards_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([DistrictId]) ON DELETE CASCADE;
                END

                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'DistrictId' AND Object_ID = Object_ID(N'Stores'))
                BEGIN
                    ALTER TABLE [Stores] ADD [DistrictId] int NULL;
                    ALTER TABLE [Stores] ADD [ProvinceId] int NULL;
                    ALTER TABLE [Stores] ADD [Latitude] decimal(9,6) NULL;
                    ALTER TABLE [Stores] ADD [Longitude] decimal(9,6) NULL;
                END

                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'DistrictId' AND Object_ID = Object_ID(N'CustomerAddresses'))
                BEGIN
                    ALTER TABLE [CustomerAddresses] ADD [DistrictId] int NULL;
                    ALTER TABLE [CustomerAddresses] ADD [ProvinceId] int NULL;
                    ALTER TABLE [CustomerAddresses] ADD [Latitude] decimal(9,6) NULL;
                    ALTER TABLE [CustomerAddresses] ADD [Longitude] decimal(9,6) NULL;
                END
            ";
            migrationBuilder.Sql(sqlSchema);
            // -----------------------------------------------------

            // NẠP DỮ LIỆU SEED TỪ FILE SQL ĐƯỢC NHÚNG VÀO DLL
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "CafeChain.Data.Seeds.vietnam_locations.sql";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception($"Không tìm thấy file seed '{resourceName}'.");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    string sqlResult = reader.ReadToEnd();
                    migrationBuilder.Sql(sqlResult);
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 5, 6, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3628), new DateTime(2026, 3, 30, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3615) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 2,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 4, 21, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3632), new DateTime(2026, 4, 5, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3631) });

            migrationBuilder.UpdateData(
                table: "Vouchers",
                keyColumn: "VoucherId",
                keyValue: 3,
                columns: new[] { "EndDate", "StartDate" },
                values: new object[] { new DateTime(2026, 6, 5, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3635), new DateTime(2026, 3, 7, 0, 11, 14, 85, DateTimeKind.Local).AddTicks(3634) });
        }
    }
}
