using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.CountryId);
                });

            migrationBuilder.CreateTable(
                name: "DrinkCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngredientId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactionTypes",
                columns: table => new
                {
                    InventoryTransactionTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactionTypes", x => x.InventoryTransactionTypeId);
                });

            migrationBuilder.CreateTable(
                name: "MemberLevels",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinPoints = table.Column<int>(type: "int", nullable: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: true),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberLevels", x => x.MemberId);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatuses",
                columns: table => new
                {
                    OrderStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatuses", x => x.OrderStatusId);
                });

            migrationBuilder.CreateTable(
                name: "OrderTypes",
                columns: table => new
                {
                    OrderTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTypes", x => x.OrderTypeId);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetOtps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    PaymentMethodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.PaymentMethodId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatuses",
                columns: table => new
                {
                    PaymentStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatuses", x => x.PaymentStatusId);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactionTypes",
                columns: table => new
                {
                    PointTransactionTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactionTypes", x => x.PointTransactionTypeId);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypes",
                columns: table => new
                {
                    ProductTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypes", x => x.ProductTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "ScopeTypes",
                columns: table => new
                {
                    ScopeTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeTypes", x => x.ScopeTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Sizes",
                columns: table => new
                {
                    SizeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.SizeId);
                });

            migrationBuilder.CreateTable(
                name: "StaffShiftStatuses",
                columns: table => new
                {
                    StaffShiftStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShiftStatuses", x => x.StaffShiftStatusId);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DebtAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierId);
                });

            migrationBuilder.CreateTable(
                name: "Toppings",
                columns: table => new
                {
                    ToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toppings", x => x.ToppingId);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxUsage = table.Column<int>(type: "int", nullable: true),
                    MaxUsagePerUser = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.VoucherId);
                    table.CheckConstraint("CK_Voucher_Date", "[StartDate] <= [EndDate]");
                    table.CheckConstraint("CK_Voucher_Discount", "(DiscountPercent IS NOT NULL AND DiscountAmount IS NULL) OR (DiscountPercent IS NULL AND DiscountAmount IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "WasteReasons",
                columns: table => new
                {
                    WasteReasonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteReasons", x => x.WasteReasonId);
                });

            migrationBuilder.CreateTable(
                name: "WheelConfigs",
                columns: table => new
                {
                    WheelConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SpinCost = table.Column<int>(type: "int", nullable: false),
                    SlotCount = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WheelConfigs", x => x.WheelConfigId);
                    table.CheckConstraint("CK_WheelConfig_Slot", "[SlotCount] IN (6,8)");
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_Customers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ProvinceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ProvinceId);
                    table.ForeignKey(
                        name: "FK_Provinces_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "CountryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitConversions",
                columns: table => new
                {
                    UnitConversionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    FromUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ratio = table.Column<decimal>(type: "decimal(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitConversions", x => x.UnitConversionId);
                    table.ForeignKey(
                        name: "FK_UnitConversions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drinks",
                columns: table => new
                {
                    DrinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProductTypeId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drinks", x => x.DrinkId);
                    table.ForeignKey(
                        name: "FK_Drinks_DrinkCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DrinkCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Drinks_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "ProductTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountRoles",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRoles", x => new { x.AccountId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AccountRoles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WheelPrizes",
                columns: table => new
                {
                    WheelPrizeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WheelConfigId = table.Column<int>(type: "int", nullable: false),
                    SlotIndex = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    IsLose = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WheelPrizes", x => x.WheelPrizeId);
                    table.CheckConstraint("CK_WheelPrize_Lose", "(IsLose = 1 AND VoucherId IS NULL) OR (IsLose = 0)");
                    table.CheckConstraint("CK_WheelPrize_Probability", "[Probability] >= 0");
                    table.ForeignKey(
                        name: "FK_WheelPrizes_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WheelPrizes_WheelConfigs_WheelConfigId",
                        column: x => x.WheelConfigId,
                        principalTable: "WheelConfigs",
                        principalColumn: "WheelConfigId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerBanks",
                columns: table => new
                {
                    CustomerBankId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBanks", x => x.CustomerBankId);
                    table.ForeignKey(
                        name: "FK_CustomerBanks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPhones",
                columns: table => new
                {
                    CustomerPhoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPhones", x => x.CustomerPhoneId);
                    table.ForeignKey(
                        name: "FK_CustomerPhones_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPoints",
                columns: table => new
                {
                    CustomerPointId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPoints", x => x.CustomerPointId);
                    table.ForeignKey(
                        name: "FK_CustomerPoints_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoucherUsages",
                columns: table => new
                {
                    VoucherUsageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsages", x => x.VoucherUsageId);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    WardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.WardId);
                    table.ForeignKey(
                        name: "FK_Wards_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkDefaultToppings",
                columns: table => new
                {
                    DrinkDefaultToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    ToppingId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkDefaultToppings", x => x.DrinkDefaultToppingId);
                    table.ForeignKey(
                        name: "FK_DrinkDefaultToppings_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkDefaultToppings_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrinkImages",
                columns: table => new
                {
                    DrinkImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkImages", x => x.DrinkImageId);
                    table.ForeignKey(
                        name: "FK_DrinkImages_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkSizes",
                columns: table => new
                {
                    DrinkSizeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    SizeId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkSizes", x => x.DrinkSizeId);
                    table.ForeignKey(
                        name: "FK_DrinkSizes_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkSizes_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "SizeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrinkToppings",
                columns: table => new
                {
                    DrinkToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    ToppingId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkToppings", x => x.DrinkToppingId);
                    table.ForeignKey(
                        name: "FK_DrinkToppings_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkToppings_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DrinkId = table.Column<int>(type: "int", nullable: true),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ParentRatingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.RatingId);
                    table.CheckConstraint("CK_Rating_Stars", "[Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Ratings_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ratings_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Ratings_ParentRatingId",
                        column: x => x.ParentRatingId,
                        principalTable: "Ratings",
                        principalColumn: "RatingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    YieldPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 100m),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DrinkId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.ForeignKey(
                        name: "FK_Recipes_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId");
                });

            migrationBuilder.CreateTable(
                name: "WheelSpins",
                columns: table => new
                {
                    WheelSpinId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    WheelConfigId = table.Column<int>(type: "int", nullable: false),
                    WheelPrizeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WheelSpins", x => x.WheelSpinId);
                    table.ForeignKey(
                        name: "FK_WheelSpins_WheelConfigs_WheelConfigId",
                        column: x => x.WheelConfigId,
                        principalTable: "WheelConfigs",
                        principalColumn: "WheelConfigId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WheelSpins_WheelPrizes_WheelPrizeId",
                        column: x => x.WheelPrizeId,
                        principalTable: "WheelPrizes",
                        principalColumn: "WheelPrizeId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    CustomerAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WardId = table.Column<int>(type: "int", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.CustomerAddressId);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "WardId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    WardId = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreId);
                    table.ForeignKey(
                        name: "FK_Stores_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "WardId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RatingImages",
                columns: table => new
                {
                    RatingImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RatingId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingImages", x => x.RatingImageId);
                    table.ForeignKey(
                        name: "FK_RatingImages_Ratings_RatingId",
                        column: x => x.RatingId,
                        principalTable: "Ratings",
                        principalColumn: "RatingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingReactions",
                columns: table => new
                {
                    RatingReactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RatingId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingReactions", x => x.RatingReactionId);
                    table.ForeignKey(
                        name: "FK_RatingReactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RatingReactions_Ratings_RatingId",
                        column: x => x.RatingId,
                        principalTable: "Ratings",
                        principalColumn: "RatingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeDetails",
                columns: table => new
                {
                    RecipeDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    ChildRecipeId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDetails", x => x.RecipeDetailId);
                    table.CheckConstraint("CK_RecipeDetail_OnlyOneSource", "(IngredientId IS NOT NULL AND ChildRecipeId IS NULL)\r\n           OR (IngredientId IS NULL AND ChildRecipeId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Recipes_ChildRecipeId",
                        column: x => x.ChildRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsOvernight = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StoreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.ShiftId);
                    table.ForeignKey(
                        name: "FK_Shifts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staffs", x => x.StaffId);
                    table.ForeignKey(
                        name: "FK_Staffs_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Staffs_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreDrinks",
                columns: table => new
                {
                    StoreDrinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreDrinks", x => x.StoreDrinkId);
                    table.ForeignKey(
                        name: "FK_StoreDrinks_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreDrinks_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreInventories",
                columns: table => new
                {
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    AvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventories", x => x.StoreInventoryId);
                    table.CheckConstraint("CK_StoreInventory_Qty", "[AvailableQty] >= 0 AND [ReservedQty] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreInventories_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreToppings",
                columns: table => new
                {
                    StoreToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ToppingId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreToppings", x => x.StoreToppingId);
                    table.ForeignKey(
                        name: "FK_StoreToppings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreToppings_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                columns: table => new
                {
                    CashSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StartCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EndCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.CashSessionId);
                    table.ForeignKey(
                        name: "FK_CashSessions_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashSessions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDocuments",
                columns: table => new
                {
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocuments", x => x.InventoryDocumentId);
                    table.ForeignKey(
                        name: "FK_InventoryDocuments_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocuments_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocuments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    OrderStatusId = table.Column<int>(type: "int", nullable: false),
                    OrderTypeId = table.Column<int>(type: "int", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VoucherDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointsUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    StoreId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_OrderStatuses_OrderStatusId",
                        column: x => x.OrderStatusId,
                        principalTable: "OrderStatuses",
                        principalColumn: "OrderStatusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_OrderTypes_OrderTypeId",
                        column: x => x.OrderTypeId,
                        principalTable: "OrderTypes",
                        principalColumn: "OrderTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK_Orders_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Stores_StoreId1",
                        column: x => x.StoreId1,
                        principalTable: "Stores",
                        principalColumn: "StoreId");
                });

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
                name: "StaffBanks",
                columns: table => new
                {
                    StaffBankId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffBanks", x => x.StaffBankId);
                    table.ForeignKey(
                        name: "FK_StaffBanks_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateTable(
                name: "StaffScopes",
                columns: table => new
                {
                    StaffScopeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ScopeTypeId = table.Column<int>(type: "int", nullable: false),
                    ScopeRefId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffScopes", x => x.StaffScopeId);
                    table.ForeignKey(
                        name: "FK_StaffScopes_ScopeTypes_ScopeTypeId",
                        column: x => x.ScopeTypeId,
                        principalTable: "ScopeTypes",
                        principalColumn: "ScopeTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffScopes_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffShifts",
                columns: table => new
                {
                    StaffShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualCheckIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualCheckOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShifts", x => x.StaffShiftId);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffShifts_StaffShiftStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StaffShiftStatuses",
                        principalColumn: "StaffShiftStatusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockImports",
                columns: table => new
                {
                    StockImportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockImports", x => x.StockImportId);
                    table.ForeignKey(
                        name: "FK_StockImports_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockImports_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockImports_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTakes",
                columns: table => new
                {
                    StockTakeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakes", x => x.StockTakeId);
                    table.ForeignKey(
                        name: "FK_StockTakes_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakes_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Wastes",
                columns: table => new
                {
                    WasteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wastes", x => x.WasteId);
                    table.ForeignKey(
                        name: "FK_Wastes_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Wastes_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDocumentDetails",
                columns: table => new
                {
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentDetails", x => x.InventoryDocumentDetailId);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentDetails_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderDetails",
                columns: table => new
                {
                    OrderDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    SizeId = table.Column<int>(type: "int", nullable: true),
                    DrinkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetails", x => x.OrderDetailId);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "SizeId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderVouchers",
                columns: table => new
                {
                    OrderVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderVouchers", x => x.OrderVoucherId);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: false),
                    PaymentStatusId = table.Column<int>(type: "int", nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_Payments_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "CashSessionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "PaymentMethodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentStatuses_PaymentStatusId",
                        column: x => x.PaymentStatusId,
                        principalTable: "PaymentStatuses",
                        principalColumn: "PaymentStatusId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    PointTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    PointTransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.PointTransactionId);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Customers_CustomerId1",
                        column: x => x.CustomerId1,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_PointTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PointTransactions_PointTransactionTypes_PointTransactionTypeId",
                        column: x => x.PointTransactionTypeId,
                        principalTable: "PointTransactionTypes",
                        principalColumn: "PointTransactionTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    StockImportId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RefType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RefId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.CheckConstraint("CK_InventoryTransaction_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransactionTypes_InventoryTransactionTypeId",
                        column: x => x.InventoryTransactionTypeId,
                        principalTable: "InventoryTransactionTypes",
                        principalColumn: "InventoryTransactionTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StockImports_StockImportId",
                        column: x => x.StockImportId,
                        principalTable: "StockImports",
                        principalColumn: "StockImportId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockImportDetails",
                columns: table => new
                {
                    StockImportDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockImportId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockImportDetails", x => x.StockImportDetailId);
                    table.CheckConstraint("CK_StockImportDetail_Price", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_StockImportDetail_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockImportDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockImportDetails_StockImports_StockImportId",
                        column: x => x.StockImportId,
                        principalTable: "StockImports",
                        principalColumn: "StockImportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTakeDetails",
                columns: table => new
                {
                    StockTakeDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTakeId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    SystemQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ActualQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeDetails", x => x.StockTakeDetailId);
                    table.CheckConstraint("CK_StockTakeDetail_Qty", "[SystemQty] >= 0 AND [ActualQty] >= 0");
                    table.ForeignKey(
                        name: "FK_StockTakeDetails_StockTakes_StockTakeId",
                        column: x => x.StockTakeId,
                        principalTable: "StockTakes",
                        principalColumn: "StockTakeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTakeDetails_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WasteDetails",
                columns: table => new
                {
                    WasteDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WasteId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    WasteReasonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteDetails", x => x.WasteDetailId);
                    table.CheckConstraint("CK_WasteDetail_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_WasteDetails_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteDetails_WasteReasons_WasteReasonId",
                        column: x => x.WasteReasonId,
                        principalTable: "WasteReasons",
                        principalColumn: "WasteReasonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteDetails_Wastes_WasteId",
                        column: x => x.WasteId,
                        principalTable: "Wastes",
                        principalColumn: "WasteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderToppings",
                columns: table => new
                {
                    OrderToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderDetailId = table.Column<int>(type: "int", nullable: false),
                    ToppingId = table.Column<int>(type: "int", nullable: false),
                    ToppingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderToppings", x => x.OrderToppingId);
                    table.ForeignKey(
                        name: "FK_OrderToppings_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderToppings_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "AccountId", "Email", "PasswordHash" },
                values: new object[,]
                {
                    { 1, "a@gmail.com", "123" },
                    { 2, "b@gmail.com", "123" },
                    { 3, "c@gmail.com", "123" },
                    { 4, "d@gmail.com", "123" },
                    { 5, "e@gmail.com", "123" },
                    { 6, "f@gmail.com", "123" },
                    { 7, "g@gmail.com", "123" },
                    { 8, "o@gmail.com", "123" },
                    { 9, "p@gmail.com", "123" },
                    { 10, "i@gmail.com", "123" },
                    { 11, "u@gmail.com", "123" },
                    { 12, "y@gmail.com", "123" },
                    { 13, "t@gmail.com", "123" },
                    { 14, "r@gmail.com", "123" },
                    { 15, "w@gmail.com", "123" },
                    { 16, "thesadboiz1712@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" },
                    { 17, "anhnttb001711@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" },
                    { 18, "AdminHeThong@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" },
                    { 19, "AdminPhuong@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" },
                    { 20, "AdminChiNhanh@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" },
                    { 21, "ThuNgan@gmail.com", "$2a$11$XfTi8A6EOmOxWskjcLpTZePuDuB9A12QhUu/KUcCtfMVT9gjzmSze" }
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "CountryId", "Name" },
                values: new object[] { 1, "Vietnam" });

            migrationBuilder.InsertData(
                table: "DrinkCategories",
                columns: new[] { "CategoryId", "Active", "Name" },
                values: new object[,]
                {
                    { 1, true, "Coffee" },
                    { 2, true, "Trà sữa" },
                    { 3, true, "Nước ngọt" }
                });

            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "IngredientId", "Active", "BaseUnit", "Code", "Name" },
                values: new object[,]
                {
                    { 1, true, "ml", "ING00001", "Cà phê" },
                    { 2, true, "ml", "ING00002", "Sữa đặc" },
                    { 3, true, "ml", "ING00003", "Trà đen" },
                    { 4, true, "g", "ING00004", "Bột sữa" },
                    { 5, true, "g", "ING00005", "Bột socola" },
                    { 6, true, "g", "ING00006", "Đường" },
                    { 7, true, "g", "ING00007", "Đá" },
                    { 8, true, "ml", "ING00008", "Syrup" },
                    { 9, true, "g", "ING00009", "Matcha" },
                    { 10, true, "ml", "ING00010", "Kem béo" },
                    { 11, true, "g", "ING00011", "Bột năng" },
                    { 12, true, "g", "ING00012", "Đường nâu" },
                    { 13, true, "ml", "ING00013", "Nước lọc" },
                    { 14, true, "g", "ING00014", "Đường trắng" }
                });

            migrationBuilder.InsertData(
                table: "InventoryTransactionTypes",
                columns: new[] { "InventoryTransactionTypeId", "Code", "IsSystem", "Name" },
                values: new object[,]
                {
                    { 1, "IMPORT", true, "Nhập kho" },
                    { 2, "EXPORT", true, "Xuất kho" },
                    { 3, "ADJUST", true, "Điều chỉnh" },
                    { 4, "WASTE", true, "Hao hụt" }
                });

            migrationBuilder.InsertData(
                table: "MemberLevels",
                columns: new[] { "MemberId", "DiscountPercent", "MaxPoints", "MinPoints", "Name" },
                values: new object[,]
                {
                    { 1, 0, 999, 0, "Bronze" },
                    { 2, 5, 4999, 1000, "Silver" },
                    { 3, 10, null, 5000, "Gold" }
                });

            migrationBuilder.InsertData(
                table: "OrderStatuses",
                columns: new[] { "OrderStatusId", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Confirmed" },
                    { 3, "Preparing" },
                    { 4, "Ready" },
                    { 5, "Completed" },
                    { 6, "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "OrderTypes",
                columns: new[] { "OrderTypeId", "Name" },
                values: new object[,]
                {
                    { 1, "Dine In" },
                    { 2, "Take Away" },
                    { 3, "Delivery" }
                });

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "PaymentMethodId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "CASH", "Tiền mặt" },
                    { 2, "BANK", "Chuyển khoản" },
                    { 3, "MOMO", "Momo" },
                    { 4, "ZALOPAY", "ZaloPay" },
                    { 5, "VNPAY", "VNPay" }
                });

            migrationBuilder.InsertData(
                table: "PaymentStatuses",
                columns: new[] { "PaymentStatusId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "PENDING", "Đang chờ" },
                    { 2, "SUCCESS", "Thành công" },
                    { 3, "FAILED", "Thất bại" },
                    { 4, "REFUND", "Đã hoàn tiền" }
                });

            migrationBuilder.InsertData(
                table: "PointTransactionTypes",
                columns: new[] { "PointTransactionTypeId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "EARN", "Tích điểm" },
                    { 2, "SPEND", "Sử dụng điểm" },
                    { 3, "EXPIRE", "Hết hạn điểm" },
                    { 4, "ADJUST", "Điều chỉnh điểm" }
                });

            migrationBuilder.InsertData(
                table: "ProductTypes",
                columns: new[] { "ProductTypeId", "Active", "Code", "Name" },
                values: new object[,]
                {
                    { 1, true, "HANDCRAFTED", "Pha chế" },
                    { 2, true, "RETAIL", "Đóng chai" }
                });

            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "RecipeId", "Active", "DrinkId", "Name", "YieldPercentage" },
                values: new object[,]
                {
                    { 1, true, null, "Recipe CF Sữa", 100m },
                    { 2, true, null, "Recipe CF Đen", 100m },
                    { 3, true, null, "Recipe Trà sữa", 100m },
                    { 4, true, null, "Recipe Trà sữa socola", 100m },
                    { 5, true, null, "Trân châu đen", 100m },
                    { 6, true, null, "Trân châu trắng", 100m }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleId", "Active", "CreatedAt", "Name" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cashier" },
                    { 2, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Store Manager" },
                    { 3, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Ward Manager" },
                    { 4, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Province Manager" },
                    { 5, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Admin System" },
                    { 6, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Customer" }
                });

            migrationBuilder.InsertData(
                table: "ScopeTypes",
                columns: new[] { "ScopeTypeId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "COUNTRY", "Country" },
                    { 2, "PROVINCE", "Province" },
                    { 3, "WARD", "Ward" },
                    { 4, "STORE", "Store" }
                });

            migrationBuilder.InsertData(
                table: "Sizes",
                columns: new[] { "SizeId", "Active", "Description", "Name" },
                values: new object[,]
                {
                    { 1, true, "Kích thước nhỏ", "S" },
                    { 2, true, "Kích thước trung bình", "M" },
                    { 3, true, "Kích thước lớn", "L" },
                    { 4, true, "Kích thước rất lớn", "XL" },
                    { 5, true, "Kích thước 150ml", "150ml" },
                    { 6, true, "Kích thước 200ml", "200ml" },
                    { 7, true, "Kích thước 250ml", "250ml" },
                    { 8, true, "Kích thước 300ml", "300ml" }
                });

            migrationBuilder.InsertData(
                table: "StaffShiftStatuses",
                columns: new[] { "StaffShiftStatusId", "Code", "IsSystem", "Name" },
                values: new object[,]
                {
                    { 1, "PLANNED", true, "Planned" },
                    { 2, "CHECKED_IN", true, "Checked In" },
                    { 3, "COMPLETED", true, "Completed" },
                    { 4, "ABSENT", true, "Absent" }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "SupplierId", "Active", "Address", "Code", "Name", "Phone" },
                values: new object[,]
                {
                    { 1, true, "Bình Dương", "SUP001", "Nhà cung cấp A", "0901111111" },
                    { 2, true, "TP HCM", "SUP002", "Nhà cung cấp B", "0902222222" },
                    { 3, true, "Đồng Nai", "SUP003", "Nhà cung cấp C", "0903333333" },
                    { 4, true, "Hà Nội", "SUP004", "Nhà cung cấp D", "0904444444" },
                    { 5, true, "Đà Nẵng", "SUP005", "Nhà cung cấp E", "0905555555" }
                });

            migrationBuilder.InsertData(
                table: "Toppings",
                columns: new[] { "ToppingId", "Active", "ImageUrl", "Name", "Price" },
                values: new object[,]
                {
                    { 1, true, "/Images/ToppingImages/tranchauden.jpg", "Trân châu đen", 5000m },
                    { 2, true, "/Images/ToppingImages/tranchautrang.jpg", "Trân châu trắng", 5000m },
                    { 3, true, "/Images/ToppingImages/phomaivien.jpg", "Phô mai viên", 7000m },
                    { 4, true, "/Images/ToppingImages/khucbachchanmeo.jpg", "Khúc bạch chân mèo", 7000m },
                    { 5, true, "/Images/ToppingImages/thachkhoaimon.jpg", "Thạch khoai môn", 6000m },
                    { 6, true, "/Images/ToppingImages/banhflan.jpg", "Bánh flan", 6000m }
                });

            migrationBuilder.InsertData(
                table: "Vouchers",
                columns: new[] { "VoucherId", "Active", "Code", "DiscountAmount", "DiscountPercent", "EndDate", "MaxDiscount", "MaxUsage", "MaxUsagePerUser", "MinOrderValue", "StartDate" },
                values: new object[,]
                {
                    { 1, true, "CAFECHAIN50", null, 50, new DateTime(2026, 5, 2, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(711), 20000m, 100, null, 40000m, new DateTime(2026, 3, 26, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(694) },
                    { 2, true, "GIAM10K", 10000m, null, new DateTime(2026, 4, 17, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(715), null, 500, null, 50000m, new DateTime(2026, 4, 1, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(715) },
                    { 3, true, "NEWUSER", null, 20, new DateTime(2026, 6, 1, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(718), 100000m, 1000, null, 0m, new DateTime(2026, 3, 3, 21, 17, 39, 838, DateTimeKind.Local).AddTicks(718) }
                });

            migrationBuilder.InsertData(
                table: "WasteReasons",
                columns: new[] { "WasteReasonId", "Active", "Code", "Name" },
                values: new object[,]
                {
                    { 1, true, "EXPIRED", "Hết hạn" },
                    { 2, true, "BROKEN", "Đổ vỡ" },
                    { 3, true, "DAMAGED", "Hư hỏng" }
                });

            migrationBuilder.InsertData(
                table: "AccountRoles",
                columns: new[] { "AccountId", "RoleId" },
                values: new object[,]
                {
                    { 16, 6 },
                    { 17, 2 },
                    { 18, 5 },
                    { 19, 4 },
                    { 20, 3 },
                    { 21, 1 }
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "CustomerId", "AccountId", "Active", "AvatarUrl", "CreatedAt", "DateOfBirth", "FullName" },
                values: new object[,]
                {
                    { 1, 1, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 12, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Văn A" },
                    { 2, 2, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Văn B" },
                    { 3, 3, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 5, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Văn C" },
                    { 4, 4, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 4, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Văn D" },
                    { 5, 5, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 2, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Văn E" },
                    { 6, 16, true, "/Images/Upload/avtdf.jpg", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2000, 12, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Thế Anh" }
                });

            migrationBuilder.InsertData(
                table: "Drinks",
                columns: new[] { "DrinkId", "Active", "CategoryId", "CreatedAt", "Description", "Name", "ProductTypeId" },
                values: new object[,]
                {
                    { 1, true, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cà phê pha với sữa đặc.", "Cà phê sữa", 1 },
                    { 2, true, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cà phê pha với nước sôi, không có sữa.", "Cà phê đen", 1 },
                    { 3, true, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Trà sữa pha với trân châu đen và đá viên.", "Trà sữa truyền thống", 1 },
                    { 4, true, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Trà sữa socola thơm ngon, béo ngậy.", "Trà sữa socola", 1 },
                    { 5, true, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sting mát lạnh", "Sting", 2 },
                    { 6, true, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Coca-cola mát lạnh", "Coca-cola", 2 }
                });

            migrationBuilder.InsertData(
                table: "Provinces",
                columns: new[] { "ProvinceId", "CountryId", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Bình Dương" },
                    { 2, 1, "Hồ Chí Minh" },
                    { 3, 1, "Đồng Nai" }
                });

            migrationBuilder.InsertData(
                table: "RecipeDetails",
                columns: new[] { "RecipeDetailId", "ChildRecipeId", "IngredientId", "Quantity", "RecipeId", "Unit" },
                values: new object[,]
                {
                    { 1, null, 1, 50m, 1, "ml" },
                    { 2, null, 2, 30m, 1, "ml" },
                    { 3, null, 7, 100m, 1, "ml" },
                    { 4, null, 1, 60m, 2, "ml" },
                    { 5, null, 7, 100m, 2, "ml" },
                    { 6, null, 3, 80m, 3, "ml" },
                    { 7, null, 4, 40m, 3, "ml" },
                    { 8, null, 6, 20m, 3, "ml" },
                    { 9, null, 7, 100m, 3, "ml" },
                    { 10, null, 3, 70m, 4, "ml" },
                    { 11, null, 4, 40m, 4, "ml" },
                    { 12, null, 5, 20m, 4, "ml" },
                    { 13, null, 6, 20m, 4, "ml" },
                    { 14, null, 7, 100m, 4, "ml" },
                    { 15, null, 11, 100m, 5, "g" },
                    { 16, null, 12, 50m, 5, "g" },
                    { 17, null, 13, 60m, 5, "ml" },
                    { 18, null, 11, 100m, 6, "g" },
                    { 19, null, 14, 40m, 6, "g" },
                    { 20, null, 13, 60m, 6, "ml" },
                    { 21, 5, null, 1m, 3, "portion" }
                });

            migrationBuilder.InsertData(
                table: "CustomerBanks",
                columns: new[] { "CustomerBankId", "AccountNumber", "BankName", "CustomerId" },
                values: new object[,]
                {
                    { 1, "123456789", "Vietcombank", 1 },
                    { 2, "987654321", "Techcombank", 2 },
                    { 3, "111222333", "BIDV", 3 },
                    { 4, "444555666", "Vietinbank", 4 },
                    { 5, "777888999", "Agribank", 5 },
                    { 6, "222333444", "Sacombank", 1 }
                });

            migrationBuilder.InsertData(
                table: "CustomerPhones",
                columns: new[] { "CustomerPhoneId", "CustomerId", "IsDefault", "Phone" },
                values: new object[,]
                {
                    { 1, 1, false, "0123456789" },
                    { 2, 2, false, "0987654321" },
                    { 3, 3, false, "0112233445" },
                    { 4, 4, false, "0223344556" },
                    { 5, 5, false, "0334455667" },
                    { 6, 1, false, "0445566778" }
                });

            migrationBuilder.InsertData(
                table: "CustomerPoints",
                columns: new[] { "CustomerPointId", "CustomerId", "Points" },
                values: new object[,]
                {
                    { 1, 1, 100 },
                    { 2, 2, 200 },
                    { 3, 3, 50 }
                });

            migrationBuilder.InsertData(
                table: "CustomerPoints",
                columns: new[] { "CustomerPointId", "CustomerId" },
                values: new object[] { 4, 4 });

            migrationBuilder.InsertData(
                table: "CustomerPoints",
                columns: new[] { "CustomerPointId", "CustomerId", "Points" },
                values: new object[] { 5, 5, 300 });

            migrationBuilder.InsertData(
                table: "DrinkDefaultToppings",
                columns: new[] { "DrinkDefaultToppingId", "DrinkId", "ToppingId" },
                values: new object[,]
                {
                    { 1, 4, 1 },
                    { 2, 4, 2 },
                    { 3, 4, 3 },
                    { 4, 4, 4 },
                    { 5, 4, 5 },
                    { 6, 4, 6 }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 1, 1, "/Images/DrinkImages/cps1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 2, 1, "/Images/DrinkImages/cps2.jpg" },
                    { 3, 1, "/Images/DrinkImages/cps3.jpg" },
                    { 4, 1, "/Images/DrinkImages/cps4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 5, 2, "/Images/DrinkImages/cpd1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 6, 2, "/Images/DrinkImages/cpd2.jpg" },
                    { 7, 2, "/Images/DrinkImages/cpd3.jpg" },
                    { 8, 2, "/Images/DrinkImages/cpd4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 9, 3, "/Images/DrinkImages/trasuatranchauden1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 10, 3, "/Images/DrinkImages/trasuatranchauden2.jpg" },
                    { 11, 3, "/Images/DrinkImages/trasuatranchauden3.jpg" },
                    { 12, 3, "/Images/DrinkImages/trasuatranchauden4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 13, 4, "/Images/DrinkImages/trasuasocola1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 14, 4, "/Images/DrinkImages/trasuasocola2.jpg" },
                    { 15, 4, "/Images/DrinkImages/trasuasocola3.jpg" },
                    { 16, 4, "/Images/DrinkImages/trasuasocola4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 17, 5, "/Images/DrinkImages/sting1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 18, 5, "/Images/DrinkImages/sting2.jpg" },
                    { 19, 5, "/Images/DrinkImages/sting3.jpg" },
                    { 20, 5, "/Images/DrinkImages/sting4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl", "IsDefault" },
                values: new object[] { 21, 6, "/Images/DrinkImages/coca1.jpg", true });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "DrinkId", "ImageUrl" },
                values: new object[,]
                {
                    { 22, 6, "/Images/DrinkImages/coca2.jpg" },
                    { 23, 6, "/Images/DrinkImages/coca3.jpg" },
                    { 24, 6, "/Images/DrinkImages/coca4.jpg" }
                });

            migrationBuilder.InsertData(
                table: "DrinkSizes",
                columns: new[] { "DrinkSizeId", "Active", "DrinkId", "Price", "SizeId" },
                values: new object[,]
                {
                    { 1, true, 1, 30000m, 1 },
                    { 3, true, 2, 22000m, 1 },
                    { 5, true, 3, 22000m, 1 },
                    { 6, true, 3, 27000m, 2 },
                    { 7, true, 3, 32000m, 3 },
                    { 8, true, 4, 25000m, 1 },
                    { 9, true, 4, 30000m, 2 },
                    { 10, true, 4, 35000m, 3 },
                    { 11, true, 5, 15000m, 5 },
                    { 12, true, 5, 20000m, 6 },
                    { 13, true, 5, 15000m, 7 },
                    { 14, true, 6, 15000m, 5 },
                    { 15, true, 6, 20000m, 6 },
                    { 16, true, 6, 25000m, 7 },
                    { 17, true, 6, 30000m, 8 }
                });

            migrationBuilder.InsertData(
                table: "DrinkToppings",
                columns: new[] { "DrinkToppingId", "Active", "DrinkId", "ToppingId" },
                values: new object[,]
                {
                    { 1, true, 3, 1 },
                    { 2, true, 3, 2 },
                    { 3, true, 3, 3 },
                    { 4, true, 3, 4 },
                    { 5, true, 3, 5 },
                    { 6, true, 3, 6 },
                    { 7, true, 4, 1 },
                    { 8, true, 4, 2 },
                    { 9, true, 4, 3 },
                    { 10, true, 4, 4 },
                    { 11, true, 4, 5 },
                    { 12, true, 4, 6 }
                });

            migrationBuilder.InsertData(
                table: "PointTransactions",
                columns: new[] { "PointTransactionId", "BalanceAfter", "CreatedAt", "CustomerId", "CustomerId1", "ExpiredAt", "OrderId", "PointTransactionTypeId", "Points" },
                values: new object[] { 5, 50, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, null, null, null, 3, 50 });

            migrationBuilder.InsertData(
                table: "Wards",
                columns: new[] { "WardId", "Name", "ProvinceId" },
                values: new object[,]
                {
                    { 1, "Phú Lợi", 1 },
                    { 2, "Lái Thiêu", 1 },
                    { 3, "Dĩ An", 1 },
                    { 4, "Phường 1", 2 },
                    { 5, "Phường 2", 2 },
                    { 6, "Phường Long Bình", 3 },
                    { 7, "Phường Trảng Dài", 3 },
                    { 8, "Phường Tân Mai", 3 },
                    { 9, "Phường Long Hưng", 3 }
                });

            migrationBuilder.InsertData(
                table: "CustomerAddresses",
                columns: new[] { "CustomerAddressId", "Address", "CustomerId", "IsDefault", "WardId" },
                values: new object[,]
                {
                    { 1, "123 Đường A", 1, true, 6 },
                    { 2, "456 Đường D", 2, true, 7 },
                    { 3, "789 Đường G", 3, true, 4 },
                    { 4, "321 Đường J", 4, true, 5 },
                    { 5, "654 Đường M", 5, true, 1 },
                    { 6, "987 Đường P", 1, false, 2 }
                });

            migrationBuilder.InsertData(
                table: "Stores",
                columns: new[] { "StoreId", "Active", "Address", "CreatedAt", "Name", "Phone", "WardId" },
                values: new object[,]
                {
                    { 1, true, "123 Đại lộ Bình Dương", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CafeChain Thủ Dầu Một", "0900000001", 1 },
                    { 2, true, "456 Nguyễn Trãi", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CafeChain Thuận An", "0900000002", 2 },
                    { 3, true, "789 Lê Hồng Phong", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CafeChain Dĩ An", "0900000003", 3 }
                });

            migrationBuilder.InsertData(
                table: "Shifts",
                columns: new[] { "ShiftId", "EndTime", "Name", "StartTime", "StoreId" },
                values: new object[,]
                {
                    { 1, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", new TimeSpan(0, 6, 0, 0, 0), 1 },
                    { 2, new TimeSpan(0, 18, 0, 0, 0), "Ca chiều", new TimeSpan(0, 12, 0, 0, 0), 1 },
                    { 3, new TimeSpan(0, 23, 0, 0, 0), "Ca tối", new TimeSpan(0, 18, 0, 0, 0), 1 },
                    { 4, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", new TimeSpan(0, 6, 0, 0, 0), 2 },
                    { 5, new TimeSpan(0, 18, 0, 0, 0), "Ca chiều", new TimeSpan(0, 12, 0, 0, 0), 2 },
                    { 6, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", new TimeSpan(0, 6, 0, 0, 0), 3 },
                    { 7, new TimeSpan(0, 23, 0, 0, 0), "Ca tối", new TimeSpan(0, 18, 0, 0, 0), 3 }
                });

            migrationBuilder.InsertData(
                table: "Staffs",
                columns: new[] { "StaffId", "AccountId", "Active", "AvatarUrl", "CreatedAt", "DateOfBirth", "FullName", "Salary", "StoreId", "TaxCode" },
                values: new object[,]
                {
                    { 1, 6, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Nguyễn Văn A", 8000000m, 1, "TAX001" },
                    { 2, 7, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Trần Thị B", 10000000m, 1, "TAX002" },
                    { 3, 8, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Lê Văn C", 12000000m, 2, "TAX003" },
                    { 4, 9, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Phạm Thị D", 14000000m, 2, "TAX004" },
                    { 5, 10, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Hoàng Văn E", 9000000m, 3, "TAX005" },
                    { 6, 11, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Đỗ Thị F", 7000000m, 3, "TAX006" },
                    { 7, 12, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Nguyễn Văn G", 8500000m, 1, "TAX007" },
                    { 8, 13, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Trần Văn H", 9500000m, 2, "TAX008" },
                    { 9, 14, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Lý Thị I", 6000000m, 3, "TAX009" },
                    { 10, 15, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Admin Tổng", 16000000m, 1, "TAX010" },
                    { 11, 17, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Admin Hệ Thống", 39999999m, 1, "TAX011" },
                    { 12, 18, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Admin Phường", 10000000000m, 1, "TAX012" },
                    { 13, 19, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Admin Tỉnh", 20000000000m, 1, "TAX013" },
                    { 14, 20, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Admin Chi Nhánh", 3000000000m, 1, "TAX014" },
                    { 15, 21, true, "/Images/Upload/avtdf.jpg", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Thu Ngân", 200000m, 1, "TAX015" }
                });

            migrationBuilder.InsertData(
                table: "StoreDrinks",
                columns: new[] { "StoreDrinkId", "Active", "DrinkId", "StoreId" },
                values: new object[,]
                {
                    { 1, true, 1, 1 },
                    { 2, true, 2, 1 },
                    { 3, true, 1, 2 },
                    { 4, true, 3, 2 },
                    { 5, true, 2, 3 },
                    { 6, true, 4, 3 }
                });

            migrationBuilder.InsertData(
                table: "StoreInventories",
                columns: new[] { "StoreInventoryId", "AvailableQty", "IngredientId", "LastUpdated", "StoreId" },
                values: new object[,]
                {
                    { 1, 100m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 2, 50m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 3, 80m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2 },
                    { 4, 60m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3 }
                });

            migrationBuilder.InsertData(
                table: "StoreToppings",
                columns: new[] { "StoreToppingId", "Active", "StoreId", "ToppingId" },
                values: new object[,]
                {
                    { 1, true, 1, 1 },
                    { 2, true, 1, 2 },
                    { 3, true, 2, 1 },
                    { 4, true, 3, 2 }
                });

            migrationBuilder.InsertData(
                table: "CashSessions",
                columns: new[] { "CashSessionId", "CloseTime", "EndCash", "OpenTime", "StaffId", "StartCash", "StoreId" },
                values: new object[] { 1, null, null, new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified), 1, 1000000m, 1 });

            migrationBuilder.InsertData(
                table: "CashSessions",
                columns: new[] { "CashSessionId", "CloseTime", "EndCash", "IsClosed", "OpenTime", "StaffId", "StartCash", "StoreId" },
                values: new object[] { 2, new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified), 800000m, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 500000m, 1 });

            migrationBuilder.InsertData(
                table: "Orders",
                columns: new[] { "OrderId", "CreatedAt", "CustomerId", "Note", "OrderStatusId", "OrderTypeId", "Source", "StaffId", "StoreId", "StoreId1", "SubTotal", "TableId", "Total" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified), 1, "", 3, 1, "POS", 1, 1, null, 45000m, 1, 45000m },
                    { 2, new DateTime(2025, 1, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), 2, "Ít đá", 2, 2, "APP", 2, 1, null, 60000m, null, 60000m },
                    { 3, new DateTime(2025, 1, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), 3, "", 1, 3, "POS", 3, 2, null, 70000m, 3, 70000m }
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
                table: "StaffBanks",
                columns: new[] { "StaffBankId", "AccountNumber", "BankName", "StaffId" },
                values: new object[,]
                {
                    { 1, "123456789", "Vietcombank", 1 },
                    { 2, "987654321", "ACB", 2 },
                    { 3, "456123789", "Techcombank", 3 }
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

            migrationBuilder.InsertData(
                table: "StaffScopes",
                columns: new[] { "StaffScopeId", "ScopeRefId", "ScopeTypeId", "StaffId" },
                values: new object[,]
                {
                    { 1, 1, 4, 1 },
                    { 2, 1, 4, 2 },
                    { 3, 1, 4, 7 },
                    { 4, 2, 4, 3 },
                    { 5, 2, 4, 4 },
                    { 6, 2, 4, 8 },
                    { 7, 3, 4, 5 },
                    { 8, 3, 4, 6 },
                    { 9, 1, 2, 9 },
                    { 10, 1, 1, 10 }
                });

            migrationBuilder.InsertData(
                table: "StaffShifts",
                columns: new[] { "StaffShiftId", "ActualCheckIn", "ActualCheckOut", "ShiftId", "StaffId", "StatusId", "WorkDate" },
                values: new object[,]
                {
                    { 1, null, null, 1, 1, 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, null, null, 2, 2, 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, null, null, 4, 3, 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "StockImports",
                columns: new[] { "StockImportId", "ImportDate", "Note", "StaffId", "StoreId", "SupplierId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập đầu ngày", 1, 1, 1 },
                    { 2, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập bổ sung", 2, 1, 2 },
                    { 3, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập nguyên liệu", 1, 2, 3 },
                    { 4, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập kho", 3, 2, 1 },
                    { 5, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập định kỳ", 2, 3, 2 },
                    { 6, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập thêm", 1, 1, 3 },
                    { 7, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập hàng", 2, 2, 4 },
                    { 8, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập tuần", 3, 3, 5 },
                    { 9, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập khẩn", 3, 1, 1 },
                    { 10, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhập cuối ngày", 1, 2, 2 }
                });

            migrationBuilder.InsertData(
                table: "InventoryTransactions",
                columns: new[] { "InventoryTransactionId", "CreatedAt", "InventoryTransactionTypeId", "Quantity", "RefId", "RefType", "StockImportId", "StoreInventoryId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 1000m, null, "IMPORT", 1, 1 },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 800m, null, "IMPORT", 2, 2 },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 200m, null, "EXPORT", 3, 3 }
                });

            migrationBuilder.InsertData(
                table: "OrderDetails",
                columns: new[] { "OrderDetailId", "DrinkId", "DrinkName", "Note", "OrderId", "Price", "Quantity", "SizeId", "SizeName" },
                values: new object[,]
                {
                    { 1, 1, "Cà phê sữa", "", 1, 25000m, 1, 2, "M" },
                    { 2, 2, "Cà phê đen", "", 1, 20000m, 1, 2, "M" },
                    { 3, 3, "Trà sữa trân châu", "Ít đá", 2, 60000m, 1, 3, "L" }
                });

            migrationBuilder.InsertData(
                table: "Payments",
                columns: new[] { "PaymentId", "Amount", "CashSessionId", "OrderId", "PaidAt", "PaymentMethodId", "PaymentStatusId", "TransactionCode" },
                values: new object[,]
                {
                    { 1, 30000m, 1, 1, new DateTime(2025, 1, 1, 8, 10, 0, 0, DateTimeKind.Unspecified), 1, 2, null },
                    { 2, 50000m, null, 2, new DateTime(2025, 1, 1, 9, 10, 0, 0, DateTimeKind.Unspecified), 3, 2, "MOMO_001" },
                    { 3, 45000m, null, 3, null, 2, 1, null },
                    { 4, 60000m, null, 1, null, 5, 3, "VNPAY_FAIL_01" },
                    { 5, 40000m, 2, 2, new DateTime(2025, 1, 1, 7, 0, 0, 0, DateTimeKind.Unspecified), 1, 4, null }
                });

            migrationBuilder.InsertData(
                table: "PointTransactions",
                columns: new[] { "PointTransactionId", "BalanceAfter", "CreatedAt", "CustomerId", "CustomerId1", "ExpiredAt", "OrderId", "PointTransactionTypeId", "Points" },
                values: new object[,]
                {
                    { 1, 50, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, new DateTime(2025, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 1, 50 },
                    { 2, 80, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, new DateTime(2025, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 1, 30 },
                    { 3, 60, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, null, null, 2, 2, 20 },
                    { 4, 100, new DateTime(2025, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, null, new DateTime(2025, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, 1, 100 }
                });

            migrationBuilder.InsertData(
                table: "StockImportDetails",
                columns: new[] { "StockImportDetailId", "IngredientId", "Quantity", "StockImportId", "UnitPrice" },
                values: new object[,]
                {
                    { 1, 1, 1000m, 1, 200000m },
                    { 2, 2, 500m, 1, 15000m },
                    { 3, 3, 800m, 2, 12000m },
                    { 4, 4, 600m, 3, 18000m },
                    { 5, 5, 700m, 4, 25000m },
                    { 6, 6, 1000m, 5, 8000m },
                    { 7, 7, 2000m, 6, 2000m },
                    { 8, 8, 300m, 7, 30000m },
                    { 9, 9, 400m, 8, 35000m },
                    { 10, 10, 500m, 9, 22000m }
                });

            migrationBuilder.InsertData(
                table: "OrderToppings",
                columns: new[] { "OrderToppingId", "OrderDetailId", "Price", "ToppingId", "ToppingName" },
                values: new object[,]
                {
                    { 1, 3, 5000m, 1, "Trân châu đen" },
                    { 2, 3, 5000m, 2, "Trân châu trắng" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRoles_RoleId",
                table: "AccountRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_StaffId",
                table: "CashSessions",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_StoreId",
                table: "CashSessions",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Name",
                table: "Countries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_WardId",
                table: "CustomerAddresses",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBanks_BankName_AccountNumber",
                table: "CustomerBanks",
                columns: new[] { "BankName", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBanks_CustomerId",
                table: "CustomerBanks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhones_CustomerId",
                table: "CustomerPhones",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhones_Phone",
                table: "CustomerPhones",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPoints_CustomerId",
                table: "CustomerPoints",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AccountId",
                table: "Customers",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkCategories_Name",
                table: "DrinkCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkDefaultToppings_DrinkId_ToppingId",
                table: "DrinkDefaultToppings",
                columns: new[] { "DrinkId", "ToppingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkDefaultToppings_ToppingId",
                table: "DrinkDefaultToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkImages_DrinkId",
                table: "DrinkImages",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_CategoryId_ProductTypeId",
                table: "Drinks",
                columns: new[] { "CategoryId", "ProductTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_Name",
                table: "Drinks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_ProductTypeId",
                table: "Drinks",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkSizes_DrinkId_SizeId",
                table: "DrinkSizes",
                columns: new[] { "DrinkId", "SizeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkSizes_SizeId",
                table: "DrinkSizes",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkToppings_DrinkId_ToppingId",
                table: "DrinkToppings",
                columns: new[] { "DrinkId", "ToppingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkToppings_ToppingId",
                table: "DrinkToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Code",
                table: "Ingredients",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_IngredientId",
                table: "InventoryDocumentDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_InventoryDocumentId",
                table: "InventoryDocumentDetails",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_Code",
                table: "InventoryDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StaffId",
                table: "InventoryDocuments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StoreId",
                table: "InventoryDocuments",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_SupplierId",
                table: "InventoryDocuments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryTransactionTypeId",
                table: "InventoryTransactions",
                column: "InventoryTransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_RefType_RefId",
                table: "InventoryTransactions",
                columns: new[] { "RefType", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StockImportId",
                table: "InventoryTransactions",
                column: "StockImportId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StoreInventoryId",
                table: "InventoryTransactions",
                column: "StoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactionTypes_Code",
                table: "InventoryTransactionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberLevels_Name",
                table: "MemberLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_DrinkId",
                table: "OrderDetails",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrderId",
                table: "OrderDetails",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_SizeId",
                table: "OrderDetails",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt",
                table: "Orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderStatusId",
                table: "Orders",
                column: "OrderStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderTypeId",
                table: "Orders",
                column: "OrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StaffId",
                table: "Orders",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId",
                table: "Orders",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId1",
                table: "Orders",
                column: "StoreId1");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatuses_Name",
                table: "OrderStatuses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_OrderDetailId_ToppingId",
                table: "OrderToppings",
                columns: new[] { "OrderDetailId", "ToppingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_ToppingId",
                table: "OrderToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTypes_Name",
                table: "OrderTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_OrderId",
                table: "OrderVouchers",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_OrderId_VoucherId",
                table: "OrderVouchers",
                columns: new[] { "OrderId", "VoucherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_VoucherId",
                table: "OrderVouchers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_CreatedAt",
                table: "PasswordResetOtps",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_Email",
                table: "PasswordResetOtps",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_Email_CodeHash_IsUsed",
                table: "PasswordResetOtps",
                columns: new[] { "Email", "CodeHash", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Code",
                table: "PaymentMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashSessionId",
                table: "Payments",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentMethodId",
                table: "Payments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentStatusId",
                table: "Payments",
                column: "PaymentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatuses_Code",
                table: "PaymentStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CustomerId",
                table: "PointTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CustomerId1",
                table: "PointTransactions",
                column: "CustomerId1");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_OrderId",
                table: "PointTransactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_PointTransactionTypeId",
                table: "PointTransactions",
                column: "PointTransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactionTypes_Code",
                table: "PointTransactionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_Code",
                table: "ProductTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_CountryId_Name",
                table: "Provinces",
                columns: new[] { "CountryId", "Name" },
                unique: true,
                filter: "[CountryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RatingImages_RatingId",
                table: "RatingImages",
                column: "RatingId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingReactions_CustomerId",
                table: "RatingReactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingReactions_RatingId",
                table: "RatingReactions",
                column: "RatingId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingReactions_RatingId_CustomerId",
                table: "RatingReactions",
                columns: new[] { "RatingId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CustomerId_DrinkId",
                table: "Ratings",
                columns: new[] { "CustomerId", "DrinkId" },
                unique: true,
                filter: "[ParentRatingId] IS NULL AND [CustomerId] IS NOT NULL AND [DrinkId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_DrinkId",
                table: "Ratings",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_ParentRatingId",
                table: "Ratings",
                column: "ParentRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_ChildRecipeId",
                table: "RecipeDetails",
                column: "ChildRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_IngredientId",
                table: "RecipeDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_RecipeId_ChildRecipeId",
                table: "RecipeDetails",
                columns: new[] { "RecipeId", "ChildRecipeId" },
                unique: true,
                filter: "[ChildRecipeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_RecipeId_IngredientId",
                table: "RecipeDetails",
                columns: new[] { "RecipeId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DrinkId",
                table: "Recipes",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScopeTypes_Code",
                table: "ScopeTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScopeTypes_Name",
                table: "ScopeTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_StoreId",
                table: "Shifts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name",
                table: "Sizes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAddresses_StaffId",
                table: "StaffAddresses",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffBanks_BankName_AccountNumber",
                table: "StaffBanks",
                columns: new[] { "BankName", "AccountNumber" },
                unique: true,
                filter: "[AccountNumber] IS NOT NULL AND [BankName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffBanks_StaffId",
                table: "StaffBanks",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPhones_StaffId",
                table: "StaffPhones",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_AccountId",
                table: "Staffs",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StoreId",
                table: "Staffs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_TaxCode",
                table: "Staffs",
                column: "TaxCode",
                filter: "[TaxCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_ScopeTypeId",
                table: "StaffScopes",
                column: "ScopeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_StaffId",
                table: "StaffScopes",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_StaffId_ScopeTypeId_ScopeRefId",
                table: "StaffScopes",
                columns: new[] { "StaffId", "ScopeTypeId", "ScopeRefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_ShiftId",
                table: "StaffShifts",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaffId_ShiftId_WorkDate",
                table: "StaffShifts",
                columns: new[] { "StaffId", "ShiftId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StatusId",
                table: "StaffShifts",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_WorkDate",
                table: "StaffShifts",
                column: "WorkDate");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShiftStatuses_Code",
                table: "StaffShiftStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockImportDetails_IngredientId",
                table: "StockImportDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockImportDetails_StockImportId_IngredientId",
                table: "StockImportDetails",
                columns: new[] { "StockImportId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_ImportDate",
                table: "StockImports",
                column: "ImportDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_StaffId",
                table: "StockImports",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_StoreId",
                table: "StockImports",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_SupplierId",
                table: "StockImports",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeDetails_StockTakeId_StoreInventoryId",
                table: "StockTakeDetails",
                columns: new[] { "StockTakeId", "StoreInventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeDetails_StoreInventoryId",
                table: "StockTakeDetails",
                column: "StoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakes_CreatedAt",
                table: "StockTakes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakes_StaffId",
                table: "StockTakes",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakes_StoreId",
                table: "StockTakes",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDrinks_DrinkId",
                table: "StoreDrinks",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDrinks_StoreId_DrinkId",
                table: "StoreDrinks",
                columns: new[] { "StoreId", "DrinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_IngredientId",
                table: "StoreInventories",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_StoreId_IngredientId",
                table: "StoreInventories",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_WardId",
                table: "Stores",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_StoreId",
                table: "StoreToppings",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_ToppingId",
                table: "StoreToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Toppings_Name",
                table: "Toppings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_IngredientId_FromUnit_ToUnit",
                table: "UnitConversions",
                columns: new[] { "IngredientId", "FromUnit", "ToUnit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_EndDate",
                table: "Vouchers",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_StartDate",
                table: "Vouchers",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_CustomerId",
                table: "VoucherUsages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_UsedAt",
                table: "VoucherUsages",
                column: "UsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_VoucherId_CustomerId",
                table: "VoucherUsages",
                columns: new[] { "VoucherId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Wards_ProvinceId_Name",
                table: "Wards",
                columns: new[] { "ProvinceId", "Name" },
                unique: true,
                filter: "[ProvinceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WasteDetails_StoreInventoryId",
                table: "WasteDetails",
                column: "StoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteDetails_WasteId_StoreInventoryId",
                table: "WasteDetails",
                columns: new[] { "WasteId", "StoreInventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteDetails_WasteReasonId",
                table: "WasteDetails",
                column: "WasteReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteReasons_Code",
                table: "WasteReasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wastes_CreatedAt",
                table: "Wastes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Wastes_StaffId",
                table: "Wastes",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Wastes_StoreId",
                table: "Wastes",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_WheelPrizes_VoucherId",
                table: "WheelPrizes",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_WheelPrizes_WheelConfigId_SlotIndex",
                table: "WheelPrizes",
                columns: new[] { "WheelConfigId", "SlotIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WheelSpins_CreatedAt",
                table: "WheelSpins",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WheelSpins_CustomerId",
                table: "WheelSpins",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WheelSpins_WheelConfigId",
                table: "WheelSpins",
                column: "WheelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_WheelSpins_WheelPrizeId",
                table: "WheelSpins",
                column: "WheelPrizeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRoles");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerBanks");

            migrationBuilder.DropTable(
                name: "CustomerPhones");

            migrationBuilder.DropTable(
                name: "CustomerPoints");

            migrationBuilder.DropTable(
                name: "DrinkDefaultToppings");

            migrationBuilder.DropTable(
                name: "DrinkImages");

            migrationBuilder.DropTable(
                name: "DrinkSizes");

            migrationBuilder.DropTable(
                name: "DrinkToppings");

            migrationBuilder.DropTable(
                name: "InventoryDocumentDetails");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "MemberLevels");

            migrationBuilder.DropTable(
                name: "OrderToppings");

            migrationBuilder.DropTable(
                name: "OrderVouchers");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "RatingImages");

            migrationBuilder.DropTable(
                name: "RatingReactions");

            migrationBuilder.DropTable(
                name: "RecipeDetails");

            migrationBuilder.DropTable(
                name: "StaffAddresses");

            migrationBuilder.DropTable(
                name: "StaffBanks");

            migrationBuilder.DropTable(
                name: "StaffPhones");

            migrationBuilder.DropTable(
                name: "StaffScopes");

            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "StockImportDetails");

            migrationBuilder.DropTable(
                name: "StockTakeDetails");

            migrationBuilder.DropTable(
                name: "StoreDrinks");

            migrationBuilder.DropTable(
                name: "StoreToppings");

            migrationBuilder.DropTable(
                name: "UnitConversions");

            migrationBuilder.DropTable(
                name: "VoucherUsages");

            migrationBuilder.DropTable(
                name: "WasteDetails");

            migrationBuilder.DropTable(
                name: "WheelSpins");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "InventoryDocuments");

            migrationBuilder.DropTable(
                name: "InventoryTransactionTypes");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PaymentStatuses");

            migrationBuilder.DropTable(
                name: "PointTransactionTypes");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "ScopeTypes");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "StaffShiftStatuses");

            migrationBuilder.DropTable(
                name: "StockImports");

            migrationBuilder.DropTable(
                name: "StockTakes");

            migrationBuilder.DropTable(
                name: "Toppings");

            migrationBuilder.DropTable(
                name: "StoreInventories");

            migrationBuilder.DropTable(
                name: "WasteReasons");

            migrationBuilder.DropTable(
                name: "Wastes");

            migrationBuilder.DropTable(
                name: "WheelPrizes");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Sizes");

            migrationBuilder.DropTable(
                name: "Drinks");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "WheelConfigs");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "OrderStatuses");

            migrationBuilder.DropTable(
                name: "OrderTypes");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "DrinkCategories");

            migrationBuilder.DropTable(
                name: "ProductTypes");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "Wards");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
