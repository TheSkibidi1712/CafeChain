using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeChain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    CouId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.CouId);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CusId);
                });

            migrationBuilder.CreateTable(
                name: "DrinkCategories",
                columns: table => new
                {
                    CatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkCategories", x => x.CatId);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    IngId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberLevels",
                columns: table => new
                {
                    MemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinPoints = table.Column<int>(type: "int", nullable: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberLevels", x => x.MemId);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatuses",
                columns: table => new
                {
                    OrSId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatuses", x => x.OrSId);
                });

            migrationBuilder.CreateTable(
                name: "OrderTypes",
                columns: table => new
                {
                    OrTId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTypes", x => x.OrTId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    PayMId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.PayMId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatuses",
                columns: table => new
                {
                    PaySId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatuses", x => x.PaySId);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScopeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sizes",
                columns: table => new
                {
                    SizId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.SizId);
                });

            migrationBuilder.CreateTable(
                name: "Toppings",
                columns: table => new
                {
                    TopId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toppings", x => x.TopId);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    VouId = table.Column<int>(type: "int", nullable: false)
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
                    table.PrimaryKey("PK_Vouchers", x => x.VouId);
                    table.CheckConstraint("CK_Voucher_Discount", "(DiscountPercent IS NOT NULL AND DiscountAmount IS NULL) OR (DiscountPercent IS NULL AND DiscountAmount IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ProId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CouId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ProId);
                    table.ForeignKey(
                        name: "FK_Provinces_Countries_CouId",
                        column: x => x.CouId,
                        principalTable: "Countries",
                        principalColumn: "CouId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerBanks",
                columns: table => new
                {
                    CusBId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBanks", x => x.CusBId);
                    table.ForeignKey(
                        name: "FK_CustomerBanks_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPhones",
                columns: table => new
                {
                    CusPId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPhones", x => x.CusPId);
                    table.ForeignKey(
                        name: "FK_CustomerPhones_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPoints",
                columns: table => new
                {
                    CusPoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPoints", x => x.CusPoId);
                    table.ForeignKey(
                        name: "FK_CustomerPoints_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drinks",
                columns: table => new
                {
                    DriId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drinks", x => x.DriId);
                    table.ForeignKey(
                        name: "FK_Drinks_DrinkCategories_CatId",
                        column: x => x.CatId,
                        principalTable: "DrinkCategories",
                        principalColumn: "CatId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VoucherUsages",
                columns: table => new
                {
                    VouUId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VouId = table.Column<int>(type: "int", nullable: false),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CustomerCusId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsages", x => x.VouUId);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Customers_CustomerCusId",
                        column: x => x.CustomerCusId,
                        principalTable: "Customers",
                        principalColumn: "CusId");
                    table.ForeignKey(
                        name: "FK_VoucherUsages_Vouchers_VouId",
                        column: x => x.VouId,
                        principalTable: "Vouchers",
                        principalColumn: "VouId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    WarId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.WarId);
                    table.ForeignKey(
                        name: "FK_Wards_Provinces_ProId",
                        column: x => x.ProId,
                        principalTable: "Provinces",
                        principalColumn: "ProId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkImages",
                columns: table => new
                {
                    DriIId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkImages", x => x.DriIId);
                    table.ForeignKey(
                        name: "FK_DrinkImages_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkSizes",
                columns: table => new
                {
                    DriSId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriId = table.Column<int>(type: "int", nullable: false),
                    SizId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkSizes", x => x.DriSId);
                    table.ForeignKey(
                        name: "FK_DrinkSizes_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkSizes_Sizes_SizId",
                        column: x => x.SizId,
                        principalTable: "Sizes",
                        principalColumn: "SizId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrinkToppings",
                columns: table => new
                {
                    DriTId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriId = table.Column<int>(type: "int", nullable: false),
                    TopId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkToppings", x => x.DriTId);
                    table.ForeignKey(
                        name: "FK_DrinkToppings_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkToppings_Toppings_TopId",
                        column: x => x.TopId,
                        principalTable: "Toppings",
                        principalColumn: "TopId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    RatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: true),
                    DriId = table.Column<int>(type: "int", nullable: true),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DrinkDriId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.RatId);
                    table.CheckConstraint("CK_Rating_Stars", "[Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Ratings_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ratings_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Drinks_DrinkDriId",
                        column: x => x.DrinkDriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId");
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    RecId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_Recipes_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    CusAId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WarId = table.Column<int>(type: "int", nullable: true),
                    WardWarId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.CusAId);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Wards_WarId",
                        column: x => x.WarId,
                        principalTable: "Wards",
                        principalColumn: "WarId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Wards_WardWarId",
                        column: x => x.WardWarId,
                        principalTable: "Wards",
                        principalColumn: "WarId");
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    WarId = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    WardWarId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoId);
                    table.ForeignKey(
                        name: "FK_Stores_Wards_WarId",
                        column: x => x.WarId,
                        principalTable: "Wards",
                        principalColumn: "WarId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Stores_Wards_WardWarId",
                        column: x => x.WardWarId,
                        principalTable: "Wards",
                        principalColumn: "WarId");
                });

            migrationBuilder.CreateTable(
                name: "RecipeDetails",
                columns: table => new
                {
                    RecDId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecId = table.Column<int>(type: "int", nullable: false),
                    IngId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IngredientIngId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDetails", x => x.RecDId);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Ingredients_IngId",
                        column: x => x.IngId,
                        principalTable: "Ingredients",
                        principalColumn: "IngId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Ingredients_IngredientIngId",
                        column: x => x.IngredientIngId,
                        principalTable: "Ingredients",
                        principalColumn: "IngId");
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Recipes_RecId",
                        column: x => x.RecId,
                        principalTable: "Recipes",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiningTables",
                columns: table => new
                {
                    TabId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    TableNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StoreStoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.TabId);
                    table.ForeignKey(
                        name: "FK_DiningTables_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiningTables_Stores_StoreStoId",
                        column: x => x.StoreStoId,
                        principalTable: "Stores",
                        principalColumn: "StoId");
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsOvernight = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.ShiId);
                    table.ForeignKey(
                        name: "FK_Shifts_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    StaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    StoreStoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staffs", x => x.StaId);
                    table.ForeignKey(
                        name: "FK_Staffs_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Staffs_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Staffs_Stores_StoreStoId",
                        column: x => x.StoreStoId,
                        principalTable: "Stores",
                        principalColumn: "StoId");
                });

            migrationBuilder.CreateTable(
                name: "StoreDrinks",
                columns: table => new
                {
                    StoDId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    DriId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreDrinks", x => x.StoDId);
                    table.ForeignKey(
                        name: "FK_StoreDrinks_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreDrinks_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreInventories",
                columns: table => new
                {
                    StoIId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    IngId = table.Column<int>(type: "int", nullable: false),
                    AvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IngredientIngId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventories", x => x.StoIId);
                    table.CheckConstraint("CK_StoreInventory_Qty", "[AvailableQty] >= 0 AND [ReservedQty] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreInventories_Ingredients_IngId",
                        column: x => x.IngId,
                        principalTable: "Ingredients",
                        principalColumn: "IngId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Ingredients_IngredientIngId",
                        column: x => x.IngredientIngId,
                        principalTable: "Ingredients",
                        principalColumn: "IngId");
                    table.ForeignKey(
                        name: "FK_StoreInventories_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreToppings",
                columns: table => new
                {
                    StoTId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    TopId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreToppings", x => x.StoTId);
                    table.ForeignKey(
                        name: "FK_StoreToppings_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreToppings_Toppings_TopId",
                        column: x => x.TopId,
                        principalTable: "Toppings",
                        principalColumn: "TopId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StartCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EndCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    StaffStaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashSessions_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashSessions_Staffs_StaffStaId",
                        column: x => x.StaffStaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId");
                    table.ForeignKey(
                        name: "FK_CashSessions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrdId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: true),
                    StoId = table.Column<int>(type: "int", nullable: false),
                    OrSId = table.Column<int>(type: "int", nullable: false),
                    OrTId = table.Column<int>(type: "int", nullable: false),
                    TabId = table.Column<int>(type: "int", nullable: true),
                    StaId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    StoreStoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrdId);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_DiningTables_TabId",
                        column: x => x.TabId,
                        principalTable: "DiningTables",
                        principalColumn: "TabId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_OrderStatuses_OrSId",
                        column: x => x.OrSId,
                        principalTable: "OrderStatuses",
                        principalColumn: "OrSId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_OrderTypes_OrTId",
                        column: x => x.OrTId,
                        principalTable: "OrderTypes",
                        principalColumn: "OrTId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Staffs_StaId",
                        column: x => x.StaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Stores_StoId",
                        column: x => x.StoId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Stores_StoreStoId",
                        column: x => x.StoreStoId,
                        principalTable: "Stores",
                        principalColumn: "StoId");
                });

            migrationBuilder.CreateTable(
                name: "StaffBanks",
                columns: table => new
                {
                    StaBId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaId = table.Column<int>(type: "int", nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffBanks", x => x.StaBId);
                    table.ForeignKey(
                        name: "FK_StaffBanks_Staffs_StaId",
                        column: x => x.StaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StaffRoles",
                columns: table => new
                {
                    StaRId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRoles", x => x.StaRId);
                    table.ForeignKey(
                        name: "FK_StaffRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffRoles_Staffs_StaId",
                        column: x => x.StaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffScopes",
                columns: table => new
                {
                    StaSId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaId = table.Column<int>(type: "int", nullable: false),
                    ScopeTypeId = table.Column<int>(type: "int", nullable: false),
                    ScopeRefId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffScopes", x => x.StaSId);
                    table.ForeignKey(
                        name: "FK_StaffScopes_ScopeTypes_ScopeTypeId",
                        column: x => x.ScopeTypeId,
                        principalTable: "ScopeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffScopes_Staffs_StaId",
                        column: x => x.StaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffShifts",
                columns: table => new
                {
                    StaSId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaId = table.Column<int>(type: "int", nullable: false),
                    ShiId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffShifts", x => x.StaSId);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Shifts_ShiId",
                        column: x => x.ShiId,
                        principalTable: "Shifts",
                        principalColumn: "ShiId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffShifts_Staffs_StaId",
                        column: x => x.StaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StaffStaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockImports_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockImports_Staffs_StaffStaId",
                        column: x => x.StaffStaId,
                        principalTable: "Staffs",
                        principalColumn: "StaId");
                    table.ForeignKey(
                        name: "FK_StockImports_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InvTId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoIId = table.Column<int>(type: "int", nullable: false),
                    InventoryTransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RefType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RefId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InvTId);
                    table.CheckConstraint("CK_InventoryTransaction_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransactionTypes_InventoryTransactionTypeId",
                        column: x => x.InventoryTransactionTypeId,
                        principalTable: "InventoryTransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StoreInventories_StoIId",
                        column: x => x.StoIId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoIId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KitchenOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenOrders_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrdId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderDetails",
                columns: table => new
                {
                    OrDId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    DriId = table.Column<int>(type: "int", nullable: false),
                    SizId = table.Column<int>(type: "int", nullable: true),
                    DrinkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetails", x => x.OrDId);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Drinks_DriId",
                        column: x => x.DriId,
                        principalTable: "Drinks",
                        principalColumn: "DriId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrdId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Sizes_SizId",
                        column: x => x.SizId,
                        principalTable: "Sizes",
                        principalColumn: "SizId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderVouchers",
                columns: table => new
                {
                    OrVId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdId = table.Column<int>(type: "int", nullable: false),
                    VouId = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderVouchers", x => x.OrVId);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Orders_OrdId",
                        column: x => x.OrdId,
                        principalTable: "Orders",
                        principalColumn: "OrdId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Vouchers_VouId",
                        column: x => x.VouId,
                        principalTable: "Vouchers",
                        principalColumn: "VouId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayMId = table.Column<int>(type: "int", nullable: false),
                    PaySId = table.Column<int>(type: "int", nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PayId);
                    table.ForeignKey(
                        name: "FK_Payments_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrdId",
                        column: x => x.OrdId,
                        principalTable: "Orders",
                        principalColumn: "OrdId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentMethods_PayMId",
                        column: x => x.PayMId,
                        principalTable: "PaymentMethods",
                        principalColumn: "PayMId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentStatuses_PaySId",
                        column: x => x.PaySId,
                        principalTable: "PaymentStatuses",
                        principalColumn: "PaySId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    PoiTId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CusId = table.Column<int>(type: "int", nullable: false),
                    OrdId = table.Column<int>(type: "int", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    PointTransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerCusId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.PoiTId);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Customers_CusId",
                        column: x => x.CusId,
                        principalTable: "Customers",
                        principalColumn: "CusId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Customers_CustomerCusId",
                        column: x => x.CustomerCusId,
                        principalTable: "Customers",
                        principalColumn: "CusId");
                    table.ForeignKey(
                        name: "FK_PointTransactions_Orders_OrdId",
                        column: x => x.OrdId,
                        principalTable: "Orders",
                        principalColumn: "OrdId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PointTransactions_PointTransactionTypes_PointTransactionTypeId",
                        column: x => x.PointTransactionTypeId,
                        principalTable: "PointTransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockImportDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockImportId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockImportDetails", x => x.Id);
                    table.CheckConstraint("CK_StockImportDetail_Price", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_StockImportDetail_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockImportDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockImportDetails_StockImports_StockImportId",
                        column: x => x.StockImportId,
                        principalTable: "StockImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderToppings",
                columns: table => new
                {
                    OrTgId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrDId = table.Column<int>(type: "int", nullable: false),
                    TopId = table.Column<int>(type: "int", nullable: false),
                    ToppingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ToppingTopId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderToppings", x => x.OrTgId);
                    table.ForeignKey(
                        name: "FK_OrderToppings_OrderDetails_OrDId",
                        column: x => x.OrDId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrDId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderToppings_Toppings_TopId",
                        column: x => x.TopId,
                        principalTable: "Toppings",
                        principalColumn: "TopId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderToppings_Toppings_ToppingTopId",
                        column: x => x.ToppingTopId,
                        principalTable: "Toppings",
                        principalColumn: "TopId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_StaffId",
                table: "CashSessions",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_StaffStaId",
                table: "CashSessions",
                column: "StaffStaId");

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
                name: "IX_CustomerAddresses_CusId",
                table: "CustomerAddresses",
                column: "CusId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_WardWarId",
                table: "CustomerAddresses",
                column: "WardWarId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_WarId",
                table: "CustomerAddresses",
                column: "WarId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBanks_BankName_AccountNumber",
                table: "CustomerBanks",
                columns: new[] { "BankName", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBanks_CusId",
                table: "CustomerBanks",
                column: "CusId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhones_CusId_Phone",
                table: "CustomerPhones",
                columns: new[] { "CusId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPoints_CusId",
                table: "CustomerPoints",
                column: "CusId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_StoId_TableNumber",
                table: "DiningTables",
                columns: new[] { "StoId", "TableNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_StoreStoId",
                table: "DiningTables",
                column: "StoreStoId");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkCategories_Name",
                table: "DrinkCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkImages_DriId",
                table: "DrinkImages",
                column: "DriId");

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_CatId",
                table: "Drinks",
                column: "CatId");

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_Name",
                table: "Drinks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkSizes_DriId_SizId",
                table: "DrinkSizes",
                columns: new[] { "DriId", "SizId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkSizes_SizId",
                table: "DrinkSizes",
                column: "SizId");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkToppings_DriId_TopId",
                table: "DrinkToppings",
                columns: new[] { "DriId", "TopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrinkToppings_TopId",
                table: "DrinkToppings",
                column: "TopId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryTransactionTypeId",
                table: "InventoryTransactions",
                column: "InventoryTransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StoIId",
                table: "InventoryTransactions",
                column: "StoIId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactionTypes_Code",
                table: "InventoryTransactionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenOrders_OrderId",
                table: "KitchenOrders",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberLevels_Name",
                table: "MemberLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_DriId",
                table: "OrderDetails",
                column: "DriId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrderId",
                table: "OrderDetails",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_SizId",
                table: "OrderDetails",
                column: "SizId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt",
                table: "Orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CusId",
                table: "Orders",
                column: "CusId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrSId",
                table: "Orders",
                column: "OrSId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrTId",
                table: "Orders",
                column: "OrTId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StaId",
                table: "Orders",
                column: "StaId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoId",
                table: "Orders",
                column: "StoId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreStoId",
                table: "Orders",
                column: "StoreStoId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TabId",
                table: "Orders",
                column: "TabId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatuses_Name",
                table: "OrderStatuses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_OrDId_TopId",
                table: "OrderToppings",
                columns: new[] { "OrDId", "TopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_TopId",
                table: "OrderToppings",
                column: "TopId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderToppings_ToppingTopId",
                table: "OrderToppings",
                column: "ToppingTopId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTypes_Name",
                table: "OrderTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_OrdId",
                table: "OrderVouchers",
                column: "OrdId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_VouId",
                table: "OrderVouchers",
                column: "VouId");

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
                name: "IX_Payments_OrdId",
                table: "Payments",
                column: "OrdId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PayMId",
                table: "Payments",
                column: "PayMId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaySId",
                table: "Payments",
                column: "PaySId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatuses_Code",
                table: "PaymentStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CusId",
                table: "PointTransactions",
                column: "CusId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CustomerCusId",
                table: "PointTransactions",
                column: "CustomerCusId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_OrdId",
                table: "PointTransactions",
                column: "OrdId");

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
                name: "IX_Provinces_CouId_Name",
                table: "Provinces",
                columns: new[] { "CouId", "Name" },
                unique: true,
                filter: "[CouId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CusId_DriId",
                table: "Ratings",
                columns: new[] { "CusId", "DriId" },
                unique: true,
                filter: "[CusId] IS NOT NULL AND [DriId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_DriId",
                table: "Ratings",
                column: "DriId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_DrinkDriId",
                table: "Ratings",
                column: "DrinkDriId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_IngId",
                table: "RecipeDetails",
                column: "IngId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_IngredientIngId",
                table: "RecipeDetails",
                column: "IngredientIngId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDetails_RecId_IngId",
                table: "RecipeDetails",
                columns: new[] { "RecId", "IngId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DriId",
                table: "Recipes",
                column: "DriId",
                unique: true);

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
                name: "IX_Shifts_StoId",
                table: "Shifts",
                column: "StoId");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name",
                table: "Sizes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffBanks_BankName_AccountNumber",
                table: "StaffBanks",
                columns: new[] { "BankName", "AccountNumber" },
                unique: true,
                filter: "[AccountNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffBanks_StaId",
                table: "StaffBanks",
                column: "StaId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_RoleId",
                table: "StaffRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_StaId_RoleId",
                table: "StaffRoles",
                columns: new[] { "StaId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_Email",
                table: "Staffs",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_RoleId",
                table: "Staffs",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StoId",
                table: "Staffs",
                column: "StoId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StoreStoId",
                table: "Staffs",
                column: "StoreStoId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_ScopeTypeId",
                table: "StaffScopes",
                column: "ScopeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_StaId",
                table: "StaffScopes",
                column: "StaId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffScopes_StaId_ScopeTypeId_ScopeRefId",
                table: "StaffScopes",
                columns: new[] { "StaId", "ScopeTypeId", "ScopeRefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_ShiId",
                table: "StaffShifts",
                column: "ShiId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_StaId_ShiId_WorkDate",
                table: "StaffShifts",
                columns: new[] { "StaId", "ShiId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffShifts_WorkDate",
                table: "StaffShifts",
                column: "WorkDate");

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
                name: "IX_StockImports_StaffId",
                table: "StockImports",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_StaffStaId",
                table: "StockImports",
                column: "StaffStaId");

            migrationBuilder.CreateIndex(
                name: "IX_StockImports_StoreId",
                table: "StockImports",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDrinks_DriId",
                table: "StoreDrinks",
                column: "DriId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDrinks_StoId_DriId",
                table: "StoreDrinks",
                columns: new[] { "StoId", "DriId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_IngId",
                table: "StoreInventories",
                column: "IngId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_IngredientIngId",
                table: "StoreInventories",
                column: "IngredientIngId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_StoId_IngId",
                table: "StoreInventories",
                columns: new[] { "StoId", "IngId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_WardWarId",
                table: "Stores",
                column: "WardWarId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_WarId",
                table: "Stores",
                column: "WarId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_StoId",
                table: "StoreToppings",
                column: "StoId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_TopId",
                table: "StoreToppings",
                column: "TopId");

            migrationBuilder.CreateIndex(
                name: "IX_Toppings_Name",
                table: "Toppings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_CusId",
                table: "VoucherUsages",
                column: "CusId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_CustomerCusId",
                table: "VoucherUsages",
                column: "CustomerCusId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_VouId_CusId",
                table: "VoucherUsages",
                columns: new[] { "VouId", "CusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wards_ProId_Name",
                table: "Wards",
                columns: new[] { "ProId", "Name" },
                unique: true,
                filter: "[ProId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerBanks");

            migrationBuilder.DropTable(
                name: "CustomerPhones");

            migrationBuilder.DropTable(
                name: "CustomerPoints");

            migrationBuilder.DropTable(
                name: "DrinkImages");

            migrationBuilder.DropTable(
                name: "DrinkSizes");

            migrationBuilder.DropTable(
                name: "DrinkToppings");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "KitchenOrders");

            migrationBuilder.DropTable(
                name: "MemberLevels");

            migrationBuilder.DropTable(
                name: "OrderToppings");

            migrationBuilder.DropTable(
                name: "OrderVouchers");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "RecipeDetails");

            migrationBuilder.DropTable(
                name: "StaffBanks");

            migrationBuilder.DropTable(
                name: "StaffRoles");

            migrationBuilder.DropTable(
                name: "StaffScopes");

            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "StockImportDetails");

            migrationBuilder.DropTable(
                name: "StoreDrinks");

            migrationBuilder.DropTable(
                name: "StoreToppings");

            migrationBuilder.DropTable(
                name: "VoucherUsages");

            migrationBuilder.DropTable(
                name: "InventoryTransactionTypes");

            migrationBuilder.DropTable(
                name: "StoreInventories");

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
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "ScopeTypes");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "StockImports");

            migrationBuilder.DropTable(
                name: "Toppings");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Sizes");

            migrationBuilder.DropTable(
                name: "Drinks");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "DiningTables");

            migrationBuilder.DropTable(
                name: "OrderStatuses");

            migrationBuilder.DropTable(
                name: "OrderTypes");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "DrinkCategories");

            migrationBuilder.DropTable(
                name: "Roles");

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
