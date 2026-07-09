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
                    RequiresPasswordChange = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowDto",
                columns: table => new
                {
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CashIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NonCashIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
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
                name: "DashboardSummaryDto",
                columns: table => new
                {
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCustomers = table.Column<int>(type: "int", nullable: false),
                    TodayOrders = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DrinkCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDocumentSnapshots",
                columns: table => new
                {
                    InventoryDocumentSnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StaffName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentSnapshots", x => x.InventoryDocumentSnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDto",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalImport = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExport = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalWaste = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
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
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BadgeColor = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    CodeHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
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
                name: "PaymentMethodDto",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalTransactions = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
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
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BadgeColor = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatuses", x => x.PaymentStatusId);
                });

            migrationBuilder.CreateTable(
                name: "PermissionGroups",
                columns: table => new
                {
                    PermissionGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGroups", x => x.PermissionGroupId);
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
                name: "RequestDeduplications",
                columns: table => new
                {
                    RequestDeduplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDeduplications", x => x.RequestDeduplicationId);
                    table.CheckConstraint("CK_RequestDeduplication_ExpiredAt", "[ExpiredAt] > [CreatedAt]");
                    table.CheckConstraint("CK_RequestDeduplication_Status", "[Status] IN ('PROCESSING', 'SUCCESS', 'FAILED', 'EXPIRED')");
                });

            migrationBuilder.CreateTable(
                name: "RevenueByStoreDto",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RevenueDto",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsStoreLevel = table.Column<bool>(type: "bit", nullable: false),
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
                    SizeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.SizeId);
                });

            migrationBuilder.CreateTable(
                name: "StaffPerformanceDto",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
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
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DebtAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierId);
                    table.CheckConstraint("CK_Supplier_DebtAmount", "[DebtAmount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingId);
                });

            migrationBuilder.CreateTable(
                name: "TopDrinkDto",
                columns: table => new
                {
                    DrinkId = table.Column<int>(type: "int", nullable: false),
                    DrinkName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalSold = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Toppings",
                columns: table => new
                {
                    ToppingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToppingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImagePublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toppings", x => x.ToppingId);
                });

            migrationBuilder.CreateTable(
                name: "TopToppingDto",
                columns: table => new
                {
                    ToppingId = table.Column<int>(type: "int", nullable: false),
                    ToppingName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalUsed = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    TransactionLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLogs", x => x.TransactionLogId);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    UnitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.UnitId);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPercent = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxUsage = table.Column<int>(type: "int", nullable: true),
                    MaxUsagePerUser = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DaysOfWeek = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartHour = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndHour = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.VoucherId);
                    table.CheckConstraint("CK_Voucher_Date", "[StartDate] <= [EndDate]");
                    table.CheckConstraint("CK_Voucher_Discount", "(DiscountPercent IS NOT NULL AND DiscountAmount IS NULL) OR (DiscountPercent IS NULL AND DiscountAmount IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "WasteDto",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    IngredientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalWasteQty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalWasteValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
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
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDocumentSnapshotDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentSnapshotId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentSnapshotDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentSnapshotDetails_InventoryDocumentSnapshots_InventoryDocumentSnapshotId",
                        column: x => x.InventoryDocumentSnapshotId,
                        principalTable: "InventoryDocumentSnapshots",
                        principalColumn: "InventoryDocumentSnapshotId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    MemberLevelId = table.Column<int>(type: "int", nullable: true),
                    TotalSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalOrders = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CurrentPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastOrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvatarPublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_Customers_MemberLevels_MemberLevelId",
                        column: x => x.MemberLevelId,
                        principalTable: "MemberLevels",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionGroupId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                    table.ForeignKey(
                        name: "FK_Permissions_PermissionGroups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "PermissionGroups",
                        principalColumn: "PermissionGroupId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Drinks",
                columns: table => new
                {
                    DrinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProductTypeId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CalculatedCogs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
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
                name: "SupplierBankAccounts",
                columns: table => new
                {
                    SupplierBankAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountHolder = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBankAccounts", x => x.SupplierBankAccountId);
                    table.ForeignKey(
                        name: "FK_SupplierBankAccounts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                columns: table => new
                {
                    SupplierContactId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.SupplierContactId);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPhones",
                columns: table => new
                {
                    SupplierPhoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPhones", x => x.SupplierPhoneId);
                    table.ForeignKey(
                        name: "FK_SupplierPhones_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngredientId);
                    table.ForeignKey(
                        name: "FK_Ingredients_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "Districts",
                columns: table => new
                {
                    DistrictId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.DistrictId);
                    table.ForeignKey(
                        name: "FK_Districts_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
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
                name: "CustomerVouchers",
                columns: table => new
                {
                    CustomerVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CollectedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UsedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerVouchers", x => x.CustomerVoucherId);
                    table.ForeignKey(
                        name: "FK_CustomerVouchers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherId",
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
                name: "AccountPermissionOverrides",
                columns: table => new
                {
                    AccountPermissionOverrideId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPermissionOverrides", x => x.AccountPermissionOverrideId);
                    table.ForeignKey(
                        name: "FK_AccountPermissionOverrides_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountPermissionOverrides_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
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
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
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
                    Active = table.Column<bool>(type: "bit", nullable: false)
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
                    RecipeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    YieldPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 100m),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentVersionId = table.Column<int>(type: "int", nullable: true),
                    DrinkId = table.Column<int>(type: "int", nullable: true),
                    SizeId = table.Column<int>(type: "int", nullable: true),
                    ToppingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.ForeignKey(
                        name: "FK_Recipe_ParentVersion",
                        column: x => x.ParentVersionId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "SizeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContactPhones",
                columns: table => new
                {
                    SupplierContactPhoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierContactId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContactPhones", x => x.SupplierContactPhoneId);
                    table.ForeignKey(
                        name: "FK_SupplierContactPhones_SupplierContacts_SupplierContactId",
                        column: x => x.SupplierContactId,
                        principalTable: "SupplierContacts",
                        principalColumn: "SupplierContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSuppliers",
                columns: table => new
                {
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSuppliers", x => x.IngredientSupplierId);
                    table.CheckConstraint("CK_IngredientSupplier_CurrentPrice", "[CurrentPrice] >= 0");
                    table.CheckConstraint("CK_IngredientSupplier_LeadTime", "[LeadTimeDays] IS NULL OR [LeadTimeDays] >= 0");
                    table.CheckConstraint("CK_IngredientSupplier_MOQ", "[MinimumOrderQuantity] IS NULL OR [MinimumOrderQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_IngredientSuppliers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSuppliers_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitConversions",
                columns: table => new
                {
                    UnitConversionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    FromUnitId = table.Column<int>(type: "int", nullable: false),
                    FromQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: false),
                    ToUnitId = table.Column<int>(type: "int", nullable: false),
                    ToQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitConversions", x => x.UnitConversionId);
                    table.CheckConstraint("CK_UnitConversion_NotSameUnit", "[FromUnitId] <> [ToUnitId]");
                    table.CheckConstraint("CK_UnitConversion_PositiveQty", "[FromQuantity] > 0 AND [ToQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_UnitConversions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnitConversions_Units_FromUnitId",
                        column: x => x.FromUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitConversions_Units_ToUnitId",
                        column: x => x.ToUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "Wards",
                columns: table => new
                {
                    WardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DistrictId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.WardId);
                    table.ForeignKey(
                        name: "FK_Wards_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingImages",
                columns: table => new
                {
                    RatingImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RatingId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    UnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDetails", x => x.RecipeDetailId);
                    table.CheckConstraint("CK_RecipeDetail_OnlyOneSource", "(IngredientId IS NOT NULL AND ChildRecipeId IS NULL)\r\n                    OR (IngredientId IS NULL AND ChildRecipeId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RecipeDetail_ChildRecipe",
                        column: x => x.ChildRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeDetail_Recipe",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeDetails_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSupplierPriceHistories",
                columns: table => new
                {
                    IngredientSupplierPriceHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSupplierPriceHistories", x => x.IngredientSupplierPriceHistoryId);
                    table.CheckConstraint("CK_IngredientSupplierPriceHistory_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_IngredientSupplierPriceHistories_IngredientSuppliers_IngredientSupplierId",
                        column: x => x.IngredientSupplierId,
                        principalTable: "IngredientSuppliers",
                        principalColumn: "IngredientSupplierId",
                        onDelete: ReferentialAction.Cascade);
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
                    DistrictId = table.Column<int>(type: "int", nullable: true),
                    ProvinceId = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                        name: "FK_CustomerAddresses_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Restrict);
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
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    WardId = table.Column<int>(type: "int", nullable: true),
                    DistrictId = table.Column<int>(type: "int", nullable: true),
                    ProvinceId = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreId);
                    table.ForeignKey(
                        name: "FK_Stores_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId");
                    table.ForeignKey(
                        name: "FK_Stores_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId");
                    table.ForeignKey(
                        name: "FK_Stores_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "WardId");
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                columns: table => new
                {
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.InventoryCostLayerId);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosTerminals",
                columns: table => new
                {
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosTerminals", x => x.TerminalId);
                    table.ForeignKey(
                        name: "FK_PosTerminals_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
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
                    IsFreeShift = table.Column<bool>(type: "bit", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    TaxCode = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    CCCD = table.Column<string>(type: "nchar(12)", fixedLength: true, maxLength: 12, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeStatus = table.Column<int>(type: "int", nullable: false),
                    SalaryType = table.Column<int>(type: "int", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Allowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProbationRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SocialInsuranceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HealthInsuranceNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    FaceDescriptor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PinHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvatarPublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    RecipeId = table.Column<int>(type: "int", nullable: true),
                    AvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    MaxNegativeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventories", x => x.StoreInventoryId);
                    table.CheckConstraint("CK_StoreInventories_XOR_Item", "([IngredientId] IS NOT NULL AND [RecipeId] IS NULL) OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL)");
                    table.CheckConstraint("CK_StoreInventory_ReservedQty", "[ReservedQty] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreInventories_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreInventorySnapshots",
                columns: table => new
                {
                    StoreInventorySnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AvgCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventorySnapshots", x => x.StoreInventorySnapshotId);
                    table.CheckConstraint("CK_StoreInventorySnapshot_AvgCost", "[AvgCost] >= 0");
                    table.CheckConstraint("CK_StoreInventorySnapshot_Quantity", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreInventorySnapshots_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventorySnapshots_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreIPs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPublicNetwork = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreIPs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreIPs_Stores_StoreId",
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsFaceVerified = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Staffs_UserId",
                        column: x => x.UserId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
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
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsProcessing = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    PartnerType = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PartnerId = table.Column<int>(type: "int", nullable: true),
                    PartnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
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
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransfers",
                columns: table => new
                {
                    InventoryTransferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FromStoreId = table.Column<int>(type: "int", nullable: false),
                    ToStoreId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CancelledByStaffId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransfers", x => x.InventoryTransferId);
                    table.CheckConstraint("CK_InventoryTransfer_DifferentStore", "[FromStoreId] <> [ToStoreId]");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Staffs_CancelledByStaffId",
                        column: x => x.CancelledByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Stores_FromStoreId",
                        column: x => x.FromStoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Stores_ToStoreId",
                        column: x => x.ToStoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    CashierId = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<int>(type: "int", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceAuditLogs_Staffs_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
                    table.ForeignKey(
                        name: "FK_InvoiceAuditLogs_Staffs_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
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
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountHolderName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
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
                name: "StaffDependents",
                columns: table => new
                {
                    StaffDependentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Relationship = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDependents", x => x.StaffDependentId);
                    table.ForeignKey(
                        name: "FK_StaffDependents_Staffs_StaffId",
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
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    IsAdHoc = table.Column<bool>(type: "bit", nullable: false),
                    CustomStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CustomEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualCheckIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualCheckOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayrollHours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                name: "StockTakeSessions",
                columns: table => new
                {
                    StockTakeSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeSessions", x => x.StockTakeSessionId);
                    table.ForeignKey(
                        name: "FK_StockTakeSessions_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakeSessions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    CashDiscrepancy = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    DiscrepancyReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsExceptionClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExceptionCloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExceptionClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ExceptionClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfflineOrderCountAtClose = table.Column<int>(type: "int", nullable: true),
                    OfflineEstimatedTotalAtClose = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OfflineCashTotalAtClose = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequiresReconciliation = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasLateOfflineSync = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LateOfflineSyncCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastLateOfflineSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PosTerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShifts", x => x.ShiftId);
                    table.ForeignKey(
                        name: "FK_WorkShifts_PosTerminals_PosTerminalId",
                        column: x => x.PosTerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Staffs_ExceptionClosedByStaffId",
                        column: x => x.ExceptionClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "InventoryDebts",
                columns: table => new
                {
                    InventoryDebtId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false),
                    PartnerType = table.Column<int>(type: "int", nullable: false),
                    PartnerId = table.Column<int>(type: "int", nullable: true),
                    PartnerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDebts", x => x.InventoryDebtId);
                    table.CheckConstraint("CK_InventoryDebt_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_InventoryDebt_Paid_Limit", "[PaidAmount] <= [Amount]");
                    table.CheckConstraint("CK_InventoryDebt_PaidAmount", "[PaidAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryDebts_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
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
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentDetails", x => x.InventoryDocumentDetailId);
                    table.CheckConstraint("CK_InventoryDocumentDetail_BaseQuantity", "[BaseQuantity] > 0");
                    table.CheckConstraint("CK_InventoryDocumentDetail_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryDocumentDetail_TotalAmount", "[TotalAmount] IS NULL OR [TotalAmount] >= 0");
                    table.CheckConstraint("CK_InventoryDocumentDetail_UnitPrice", "[UnitPrice] IS NULL OR [UnitPrice] >= 0");
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
                    table.ForeignKey(
                        name: "FK_InventoryDocumentDetails_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDetails",
                columns: table => new
                {
                    InventoryTransferDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SourceBeforeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SourceAfterQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DestinationBeforeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DestinationAfterQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransferDetails", x => x.InventoryTransferDetailId);
                    table.CheckConstraint("CK_InventoryTransferDetail_BaseQuantity", "[BaseQuantity] > 0");
                    table.CheckConstraint("CK_InventoryTransferDetail_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryTransferDetail_UnitPrice", "[UnitPrice] IS NULL OR [UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTakeDetails",
                columns: table => new
                {
                    StockTakeDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTakeSessionId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeDetails", x => x.StockTakeDetailId);
                    table.CheckConstraint("CK_StockTakeDetail_ActualQuantity", "[ActualQuantity] >= 0");
                    table.CheckConstraint("CK_StockTakeDetail_SystemQuantity", "[SystemQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_StockTakeDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakeDetails_StockTakeSessions_StockTakeSessionId",
                        column: x => x.StockTakeSessionId,
                        principalTable: "StockTakeSessions",
                        principalColumn: "StockTakeSessionId",
                        onDelete: ReferentialAction.Cascade);
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
                    PaymentStatusId = table.Column<int>(type: "int", nullable: false),
                    OrderTypeId = table.Column<int>(type: "int", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    WorkShiftId = table.Column<int>(type: "int", nullable: true),
                    ClientOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiverPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VoucherDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointsUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
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
                        name: "FK_Orders_PaymentStatuses_PaymentStatusId",
                        column: x => x.PaymentStatusId,
                        principalTable: "PaymentStatuses",
                        principalColumn: "PaymentStatusId",
                        onDelete: ReferentialAction.Cascade);
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
                        name: "FK_Orders_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                columns: table => new
                {
                    OtpChallengeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    WorkShiftId = table.Column<int>(type: "int", nullable: true),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ApproverStaffId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ResendCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.OtpChallengeId);
                    table.ForeignKey(
                        name: "FK_OtpChallenges_Staffs_ApproverStaffId",
                        column: x => x.ApproverStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtpChallenges_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtpChallenges_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtpChallenges_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostAllocations",
                columns: table => new
                {
                    InventoryCostAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: false),
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostAllocations", x => x.InventoryCostAllocationId);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_InventoryCostLayers_InventoryCostLayerId",
                        column: x => x.InventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_InventoryDocumentDetails_InventoryDocumentDetailId",
                        column: x => x.InventoryDocumentDetailId,
                        principalTable: "InventoryDocumentDetails",
                        principalColumn: "InventoryDocumentDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StockStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BeforeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AfterQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    ReferenceOrderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.CheckConstraint("CK_InventoryTransaction_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryTransaction_TotalCost", "[TotalCost] IS NULL OR [TotalCost] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_UnitCost", "[UnitCost] IS NULL OR [UnitCost] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Orders_ReferenceOrderId",
                        column: x => x.ReferenceOrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
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
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "AccountId", "Active", "CreatedAt", "Email", "LockoutEnd", "PasswordHash", "RequiresPasswordChange" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "owner@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 2, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "areamanager@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 3, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "storemanager@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 4, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "salesstaff@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 5, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "accountantwarehouse@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 6, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "systemadmin@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true },
                    { 7, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "khachhang@gmail.com", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true }
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "CountryId", "Name" },
                values: new object[] { 1, "Vietnam" });

            migrationBuilder.InsertData(
                table: "DrinkCategories",
                columns: new[] { "CategoryId", "Active", "CategoryCode", "Icon", "Name" },
                values: new object[,]
                {
                    { 1, true, "COFFEE", "☕", "Coffee" },
                    { 2, true, "TRASUA", "🧋", "Trà sữa" },
                    { 3, true, "NUOCNGOT", "🥤", "Nước ngọt" }
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
                columns: new[] { "OrderStatusId", "BadgeColor", "Name" },
                values: new object[,]
                {
                    { 1, "badge bg-secondary", "Chờ xác nhận" },
                    { 2, "badge bg-primary", "Đang pha chế" },
                    { 3, "badge bg-info text-dark", "Chờ lấy hàng" },
                    { 4, "badge bg-warning text-dark", "Đang giao hàng" },
                    { 5, "badge bg-success", "Hoàn thành" },
                    { 6, "badge bg-danger", "Đã hủy" },
                    { 7, "badge bg-warning", "Chờ thanh toán" }
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
                columns: new[] { "PaymentStatusId", "BadgeColor", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "badge bg-warning text-dark", "UNPAID", "Chưa thanh toán" },
                    { 2, "badge bg-success", "PAID", "Đã thanh toán" },
                    { 3, "badge bg-info text-dark", "REFUNDED", "Đã hoàn tiền" },
                    { 4, "badge bg-danger", "FAILED", "Lỗi thanh toán" }
                });

            migrationBuilder.InsertData(
                table: "PermissionGroups",
                columns: new[] { "PermissionGroupId", "Active", "Code", "DisplayOrder", "Name" },
                values: new object[,]
                {
                    { 1, true, "DRINK", 1, "Quản lý đồ uống" },
                    { 2, true, "TOPPING", 2, "Quản lý Topping" },
                    { 3, true, "ORDER", 3, "Quản lý đơn hàng" },
                    { 4, true, "CUSTOMER", 4, "Quản lý khách hàng" },
                    { 5, true, "SYSTEM", 999, "Hệ thống" }
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
                table: "Roles",
                columns: new[] { "RoleId", "Active", "CreatedAt", "IsStoreLevel", "Name" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Chủ doanh nghiệp" },
                    { 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Quản lý vùng" },
                    { 3, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Quản lý chi nhánh" },
                    { 4, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Nhân viên bán hàng" },
                    { 5, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Kế toán/kho" },
                    { 6, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Quản trị hệ thống" },
                    { 7, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Khách hàng" }
                });

            migrationBuilder.InsertData(
                table: "ScopeTypes",
                columns: new[] { "ScopeTypeId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "COUNTRY", "Country" },
                    { 2, "PROVINCE", "Province" },
                    { 3, "DISTRICT", "District" },
                    { 4, "WARD", "Ward" },
                    { 5, "STORE", "Store" }
                });

            migrationBuilder.InsertData(
                table: "Sizes",
                columns: new[] { "SizeId", "Active", "Description", "Name", "SizeCode" },
                values: new object[,]
                {
                    { 1, true, "Kích thước nhỏ", "S", "S" },
                    { 2, true, "Kích thước trung bình", "M", "M" },
                    { 3, true, "Kích thước lớn", "L", "L" },
                    { 4, true, "Kích thước rất lớn", "XL", "XL" },
                    { 5, true, "Kích thước 150ml", "150ml", "150ML" },
                    { 6, true, "Kích thước 200ml", "200ml", "200ML" },
                    { 7, true, "Kích thước 250ml", "250ml", "250ML" },
                    { 8, true, "Kích thước 300ml", "300ml", "300ML" }
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
                table: "Stores",
                columns: new[] { "StoreId", "Active", "Address", "CreatedAt", "DistrictId", "Latitude", "Longitude", "Name", "Phone", "ProvinceId", "WardId" },
                values: new object[,]
                {
                    { 1, true, "123 Đại lộ Bình Dương", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CafeChain Thủ Dầu Một", "0900000001", null, null },
                    { 2, true, "456 Nguyễn Trãi", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CafeChain Thuận An", "0900000002", null, null },
                    { 3, true, "789 Lê Hồng Phong", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CafeChain Dĩ An", "0900000003", null, null }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "SupplierId", "Active", "Address", "Code", "CreatedAt", "Name", "Note", "Status", "TaxCode", "Website" },
                values: new object[,]
                {
                    { 1, true, "Bình Dương", "SUP001", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp A", "Nhà cung cấp nguyên liệu chính", 1, "0101234567", "https://supA.com" },
                    { 2, true, "TP HCM", "SUP002", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp B", "Nhà cung cấp sữa và kem", 1, "0201234567", "https://supB.com" },
                    { 3, true, "Đồng Nai", "SUP003", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp C", "Nhà cung cấp cà phê", 1, "0301234567", "https://supC.com" },
                    { 4, true, "Hà Nội", "SUP004", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp D", "Nhà cung cấp syrup và trà", 1, "0401234567", "https://supD.com" }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "SupplierId", "Active", "Address", "Code", "CreatedAt", "DebtAmount", "Name", "Note", "Status", "TaxCode", "Website" },
                values: new object[] { 5, true, "Đà Nẵng", "SUP005", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 100000m, "Nhà cung cấp E", "Nhà cung cấp matcha", 1, "0501234567", "https://supE.com" });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "SettingId", "Description", "SettingKey", "SettingValue" },
                values: new object[,]
                {
                    { 1, "Toạ độ trung tâm mặc định (VD: TPHCM - 10.8231, 106.6297)", "Map_Default_Center", "10.8231, 106.6297" },
                    { 2001, "Cho phép tồn kho âm có kiểm soát.", "inventory_allow_negative_stock", "false" },
                    { 2002, "Yêu cầu quản lý/admin xác nhận khi giao dịch làm âm kho.", "inventory_require_manager_approval_for_negative_stock", "false" },
                    { 2003, "Ngưỡng âm kho mặc định nếu tồn kho nguyên liệu không cấu hình riêng.", "inventory_default_max_negative_quantity", "0" }
                });

            migrationBuilder.InsertData(
                table: "Toppings",
                columns: new[] { "ToppingId", "Active", "ImagePublicId", "ImageUrl", "Name", "Price", "ToppingCode" },
                values: new object[,]
                {
                    { 1, true, "tranchauden_ftddpx", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804079/tranchauden_ftddpx.jpg", "Trân châu đen", 5000m, "TC_DEN" },
                    { 2, true, "tranchautrang_c2pylw", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804079/tranchautrang_c2pylw.jpg", "Trân châu trắng", 5000m, "TC_TRANG" },
                    { 3, true, "phomaivien_ujfenk", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804075/phomaivien_ujfenk.jpg", "Phô mai viên", 7000m, "PM_VIEN" },
                    { 4, true, "khucbachchanmeo_r2fxzd", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804082/khucbachchanmeo_r2fxzd.jpg", "Khúc bạch chân mèo", 7000m, "KB_CM" },
                    { 5, true, "thachkhoaimon_fwpprq", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804078/thachkhoaimon_fwpprq.jpg", "Thạch khoai môn", 6000m, "TH_KM" },
                    { 6, true, "banhflan_zndwvl", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804080/banhflan_zndwvl.jpg", "Bánh flan", 6000m, "BH_FLAN" }
                });

            migrationBuilder.InsertData(
                table: "Units",
                columns: new[] { "UnitId", "Active", "Name", "Type", "UnitCode" },
                values: new object[,]
                {
                    { 1, true, "Gram", 1, "g" },
                    { 2, true, "Kilogram", 1, "kg" },
                    { 3, true, "Milliliter", 2, "ml" },
                    { 4, true, "Liter", 2, "l" },
                    { 5, true, "Ounce", 2, "oz" },
                    { 6, true, "Cup", 2, "cup" },
                    { 7, true, "Tablespoon", 2, "tbsp" },
                    { 8, true, "Teaspoon", 2, "tsp" },
                    { 9, true, "Piece", 3, "pcs" },
                    { 10, true, "Bottle", 3, "bottle" },
                    { 11, true, "Can", 3, "can" },
                    { 12, true, "Pack", 3, "pack" }
                });

            migrationBuilder.InsertData(
                table: "Vouchers",
                columns: new[] { "VoucherId", "Active", "Code", "DaysOfWeek", "Description", "DiscountAmount", "DiscountPercent", "EndDate", "EndHour", "MaxDiscount", "MaxUsage", "MaxUsagePerUser", "MinOrderValue", "StartDate", "StartHour", "Title" },
                values: new object[,]
                {
                    { 1, true, "CAFECHAIN50", null, null, null, 50, new DateTime(2026, 8, 8, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(356), null, 20000m, 100, null, 40000m, new DateTime(2026, 7, 2, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(347), null, null },
                    { 2, true, "GIAM10K", null, null, 10000m, null, new DateTime(2026, 7, 24, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(363), null, null, 500, null, 50000m, new DateTime(2026, 7, 8, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(363), null, null },
                    { 3, true, "NEWUSER", null, null, null, 20, new DateTime(2026, 9, 7, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(365), null, 100000m, 1000, null, 0m, new DateTime(2026, 6, 9, 23, 50, 24, 153, DateTimeKind.Local).AddTicks(365), null, null }
                });

            migrationBuilder.InsertData(
                table: "AccountRoles",
                columns: new[] { "AccountId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 4, 4 },
                    { 5, 5 },
                    { 6, 6 },
                    { 7, 7 }
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "CustomerId", "AccountId", "Active", "AvatarPublicId", "AvatarUrl", "Category", "CreatedAt", "CustomerCode", "DateOfBirth", "DeletedAt", "FullName", "Gender", "LastOrderDate", "MemberLevelId", "UpdatedAt" },
                values: new object[] { 1, 7, true, "avtdf_r3cjq5", "https://res.cloudinary.com/dzfizobk8/image/upload/v1779801172/avtdf_r3cjq5.jpg", 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CUS000111", new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Khách Hàng Mới", 1, null, null, null });

            migrationBuilder.InsertData(
                table: "Drinks",
                columns: new[] { "DrinkId", "Active", "CalculatedCogs", "CategoryId", "CreatedAt", "Description", "DrinkCode", "Name", "ProductTypeId" },
                values: new object[,]
                {
                    { 1, true, 0m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cà phê pha với sữa đặc.", "CF_Sua", "Cà phê sữa", 1 },
                    { 2, true, 0m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cà phê pha với nước sôi, không có sữa.", "CF_Den", "Cà phê đen", 1 },
                    { 3, true, 0m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Trà sữa pha với trân châu đen và đá viên.", "TS_TruyenThong", "Trà sữa truyền thống", 1 },
                    { 4, true, 0m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Trà sữa socola thơm ngon, béo ngậy.", "TS_Socola", "Trà sữa socola", 1 },
                    { 5, true, 0m, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sting mát lạnh", "STING", "Sting", 2 },
                    { 6, true, 0m, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Coca-cola mát lạnh", "COCA", "Coca-cola", 2 }
                });

            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "IngredientId", "Active", "BaseUnitId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, true, 1, "ING00001", "Cà phê hạt Robusta 1kg" },
                    { 2, true, 3, "ING00002", "Sữa đặc Ông Thọ Vinamilk 380g" },
                    { 3, true, 1, "ING00003", "Trà đen Lipton hộp 100 túi" },
                    { 4, true, 1, "ING00004", "Bột sữa B-One 1kg" },
                    { 5, true, 1, "ING00005", "Bột cacao Van Houten 1kg" },
                    { 6, true, 1, "ING00006", "Đường trắng Biên Hòa 1kg" },
                    { 7, true, 1, "ING00007", "Đá viên 1kg" },
                    { 8, true, 3, "ING00008", "Syrup Torani Vanilla 750ml" },
                    { 9, true, 1, "ING00009", "Matcha Nhật Bản 500g" },
                    { 10, true, 3, "ING00010", "Kem béo Rich's 1L" },
                    { 11, true, 1, "ING00011", "Bột năng Vĩnh Thuận 400g" },
                    { 12, true, 1, "ING00012", "Đường nâu Hàn Quốc 1kg" },
                    { 13, true, 3, "ING00013", "Nước lọc Lavie 500ml" }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "Action", "Active", "Code", "CreatedAt", "Description", "Name", "PermissionGroupId" },
                values: new object[,]
                {
                    { 1, "View", true, "Drink.View", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Xem đồ uống", 1 },
                    { 2, "Create", true, "Drink.Create", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Thêm đồ uống", 1 },
                    { 3, "Update", true, "Drink.Update", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Cập nhật đồ uống", 1 },
                    { 4, "Delete", true, "Drink.Delete", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Xóa đồ uống", 1 },
                    { 100, "Manage", true, "System.Permission.Manage", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Quản lý phân quyền", 5 }
                });

            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "RecipeId", "Active", "DrinkId", "EffectiveDate", "Name", "ParentVersionId", "RecipeCode", "SizeId", "Status", "ToppingId", "YieldPercentage" },
                values: new object[,]
                {
                    { 5, true, null, null, "Trân châu đen", null, "RCP_TC_DEN", null, "Active", 1, 100m },
                    { 6, true, null, null, "Trân châu trắng", null, "RCP_TC_TRANG", null, "Active", 2, 100m }
                });

            migrationBuilder.InsertData(
                table: "Shifts",
                columns: new[] { "ShiftId", "Duration", "EndTime", "IsFreeShift", "Name", "Notes", "StartTime", "StoreId" },
                values: new object[,]
                {
                    { 1, null, new TimeSpan(0, 12, 0, 0, 0), false, "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 1 },
                    { 2, null, new TimeSpan(0, 18, 0, 0, 0), false, "Ca chiều", null, new TimeSpan(0, 12, 0, 0, 0), 1 },
                    { 3, null, new TimeSpan(0, 23, 0, 0, 0), false, "Ca tối", null, new TimeSpan(0, 18, 0, 0, 0), 1 },
                    { 4, null, new TimeSpan(0, 12, 0, 0, 0), false, "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 2 },
                    { 5, null, new TimeSpan(0, 18, 0, 0, 0), false, "Ca chiều", null, new TimeSpan(0, 12, 0, 0, 0), 2 },
                    { 6, null, new TimeSpan(0, 12, 0, 0, 0), false, "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 3 },
                    { 7, null, new TimeSpan(0, 23, 0, 0, 0), false, "Ca tối", null, new TimeSpan(0, 18, 0, 0, 0), 3 }
                });

            migrationBuilder.InsertData(
                table: "Staffs",
                columns: new[] { "StaffId", "AccountId", "Active", "Allowance", "AvatarPublicId", "AvatarUrl", "BaseSalary", "CCCD", "CreatedAt", "DateOfBirth", "EmployeeStatus", "FaceDescriptor", "FullName", "Gender", "HealthInsuranceNumber", "OvertimeRate", "PinHash", "ProbationRate", "SalaryType", "SocialInsuranceNumber", "StartDate", "StoreId", "TaxCode" },
                values: new object[,]
                {
                    { 1, 1, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 100000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Chủ doanh nghiệp", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX101" },
                    { 2, 2, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 45000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Quản lý vùng TP.HCM", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX102" },
                    { 3, 3, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 25000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Quản lý chi nhánh Quận 1", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX103" },
                    { 4, 4, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 9000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Nhân viên bán hàng", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX104" },
                    { 5, 5, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 15000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Nhân viên kế toán kho", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX105" },
                    { 6, 6, true, 0m, "staffs/default-avatar", "/Images/Upload/avtdf.jpg", 35000000m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, null, "Quản trị hệ thống", 0, null, 0m, null, 0m, 0, null, null, 1, "TAX106" }
                });

            migrationBuilder.InsertData(
                table: "StoreIPs",
                columns: new[] { "Id", "CreatedAt", "IPAddress", "IsActive", "Notes", "StoreId" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "192.168.1.*", true, "Mạng LAN cửa hàng 1", 1 });

            migrationBuilder.InsertData(
                table: "StoreIPs",
                columns: new[] { "Id", "CreatedAt", "IPAddress", "IsActive", "IsPublicNetwork", "Notes", "StoreId" },
                values: new object[] { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "171.244.10.15", true, true, "WAN Cửa hàng 1", 1 });

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
                table: "SupplierBankAccounts",
                columns: new[] { "SupplierBankAccountId", "AccountHolder", "AccountNumber", "Active", "BankName", "Branch", "IsPrimary", "SupplierId" },
                values: new object[,]
                {
                    { 1, "NCC A", "111111111", true, "Vietcombank", "Bình Dương", true, 1 },
                    { 2, "NCC B", "222222222", true, "ACB", "TP HCM", true, 2 },
                    { 3, "NCC C", "333333333", true, "Techcombank", "Đồng Nai", true, 3 },
                    { 4, "NCC D", "444444444", true, "BIDV", "Hà Nội", true, 4 },
                    { 5, "NCC E", "555555555", true, "MB Bank", "Đà Nẵng", true, 5 }
                });

            migrationBuilder.InsertData(
                table: "SupplierContacts",
                columns: new[] { "SupplierContactId", "Active", "Email", "IsPrimary", "Name", "Note", "Position", "SupplierId" },
                values: new object[,]
                {
                    { 1, true, "a@supplier.com", true, "Nguyễn Văn A", "Liên hệ chính", "Manager", 1 },
                    { 2, true, "b@supplier.com", true, "Trần Văn B", "Phụ trách bán hàng", "Sales", 2 },
                    { 3, true, "c@supplier.com", true, "Lê Văn C", "Chủ doanh nghiệp", "Owner", 3 },
                    { 4, true, "d@supplier.com", true, "Phạm Văn D", "Giám đốc", "Director", 4 },
                    { 5, true, "e@supplier.com", true, "Hoàng Văn E", "Quản lý kinh doanh", "Manager", 5 }
                });

            migrationBuilder.InsertData(
                table: "SupplierPhones",
                columns: new[] { "SupplierPhoneId", "Description", "IsPrimary", "PhoneNumber", "SupplierId" },
                values: new object[] { 1, "Hotline", true, "0901111111", 1 });

            migrationBuilder.InsertData(
                table: "SupplierPhones",
                columns: new[] { "SupplierPhoneId", "Description", "PhoneNumber", "SupplierId" },
                values: new object[] { 2, "Kho hàng", "0901111112", 1 });

            migrationBuilder.InsertData(
                table: "SupplierPhones",
                columns: new[] { "SupplierPhoneId", "Description", "IsPrimary", "PhoneNumber", "SupplierId" },
                values: new object[,]
                {
                    { 3, "Hotline", true, "0902222222", 2 },
                    { 4, "Hotline", true, "0903333333", 3 },
                    { 5, "Hotline", true, "0904444444", 4 },
                    { 6, "Hotline", true, "0905555555", 5 }
                });

            migrationBuilder.InsertData(
                table: "CashSessions",
                columns: new[] { "CashSessionId", "CloseTime", "EndCash", "OpenTime", "StaffId", "StartCash", "StoreId" },
                values: new object[] { 1, null, null, new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified), 5, 1000000m, 1 });

            migrationBuilder.InsertData(
                table: "CashSessions",
                columns: new[] { "CashSessionId", "CloseTime", "EndCash", "IsClosed", "OpenTime", "StaffId", "StartCash", "StoreId" },
                values: new object[] { 2, new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Unspecified), 800000m, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, 500000m, 1 });

            migrationBuilder.InsertData(
                table: "CustomerAddresses",
                columns: new[] { "CustomerAddressId", "Address", "CustomerId", "DistrictId", "IsDefault", "IsDeleted", "Latitude", "Longitude", "ProvinceId", "WardId" },
                values: new object[] { 1, "987 Đường P", 1, null, false, false, null, null, null, null });

            migrationBuilder.InsertData(
                table: "CustomerBanks",
                columns: new[] { "CustomerBankId", "AccountNumber", "BankName", "CustomerId" },
                values: new object[] { 1, "111222333444", "Vietcombank", 1 });

            migrationBuilder.InsertData(
                table: "CustomerPhones",
                columns: new[] { "CustomerPhoneId", "CustomerId", "IsDefault", "Phone" },
                values: new object[] { 1, 1, false, "0900111222" });

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
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803239/cps1_ip9ciu.jpg", true, "cps1_ip9ciu" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803240/cps2_zd0pyd.jpg", "cps2_zd0pyd" },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803240/cps3_guo9om.jpg", "cps3_guo9om" },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803241/cps4_koocly.jpg", "cps4_koocly" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803225/cpd1_cgkole.jpg", true, "cpd1_cgkole" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803236/cpd2_xgqlei.jpg", "cpd2_xgqlei" },
                    { 7, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803237/cpd3_dwyqpv.jpg", "cpd3_dwyqpv" },
                    { 8, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803238/cpd4_xphst1.jpg", "cpd4_xphst1" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 9, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803061/trasuatranchauden1_kekbpp.jpg", true, "trasuatranchauden1_kekbpp" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803062/trasuatranchauden2_m4kkru.jpg", "trasuatranchauden2_m4kkru" },
                    { 11, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803062/trasuatranchauden3_pcmlfn.jpg", "trasuatranchauden3_pcmlfn" },
                    { 12, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803063/trasuatranchauden4_cngwyr.jpg", "trasuatranchauden4_cngwyr" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 13, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802891/trasuasocola1_hc4t3p.jpg", true, "trasuasocola1_hc4t3p" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 14, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802891/trasuasocola2_m9yp1i.jpg", "trasuasocola2_m9yp1i" },
                    { 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802892/trasuasocola3_t8nr2b.jpg", "trasuasocola3_t8nr2b" },
                    { 16, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802950/trasuasocola4_kju0s7.jpg", "trasuasocola4_kju0s7" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 17, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803393/sting1_tcita4.jpg", true, "sting1_tcita4" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 18, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803314/sting2_axipva.jpg", "sting2_axipva" },
                    { 19, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803314/sting3_rv03ev.jpg", "sting3_rv03ev" },
                    { 20, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803316/sting4_yzaesh.jpg", "sting4_yzaesh" }
                });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "IsDefault", "PublicId" },
                values: new object[] { 21, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803080/coca1_qum0eb.jpg", true, "coca1_qum0eb" });

            migrationBuilder.InsertData(
                table: "DrinkImages",
                columns: new[] { "DrinkImageId", "CreatedAt", "DrinkId", "ImageUrl", "PublicId" },
                values: new object[,]
                {
                    { 22, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803081/coca2_ctcrt0.jpg", "coca2_ctcrt0" },
                    { 23, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803082/coca3_mp28bz.jpg", "coca3_mp28bz" },
                    { 24, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803082/coca4_xbh74i.jpg", "coca4_xbh74i" }
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
                    { 13, true, 5, 25000m, 7 },
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
                    { 1, false, 3, 1 },
                    { 2, false, 3, 2 },
                    { 3, false, 3, 3 },
                    { 4, false, 3, 4 },
                    { 5, false, 3, 5 },
                    { 6, false, 3, 6 },
                    { 7, false, 4, 1 },
                    { 8, false, 4, 2 },
                    { 9, false, 4, 3 },
                    { 10, false, 4, 4 },
                    { 11, false, 4, 5 },
                    { 12, false, 4, 6 }
                });

            migrationBuilder.InsertData(
                table: "IngredientSuppliers",
                columns: new[] { "IngredientSupplierId", "Active", "CurrentPrice", "IngredientId", "IsPrimary", "LeadTimeDays", "MinimumOrderQuantity", "Note", "SupplierId", "UnitId" },
                values: new object[,]
                {
                    { 1, true, 22000m, 6, true, 1, 1m, "Đường Biên Hòa", 1, 2 },
                    { 2, true, 27000m, 2, true, 2, 24m, "Sữa đặc Ông Thọ", 2, 3 },
                    { 3, true, 140000m, 1, true, 3, 5m, "Cà phê hạt", 3, 2 },
                    { 4, true, 250000m, 8, true, 4, 6m, "Syrup Torani", 4, 3 },
                    { 5, true, 95000m, 10, true, 2, 12m, "Kem béo Rich", 2, 4 },
                    { 6, true, 450000m, 9, true, 5, 1m, "Matcha Nhật", 5, 1 }
                });

            migrationBuilder.InsertData(
                table: "IngredientSuppliers",
                columns: new[] { "IngredientSupplierId", "Active", "CurrentPrice", "IngredientId", "LeadTimeDays", "MinimumOrderQuantity", "Note", "SupplierId", "UnitId" },
                values: new object[,]
                {
                    { 7, true, 180000m, 5, 3, 2m, "Bột cacao", 3, 2 },
                    { 8, true, 85000m, 4, 2, 2m, "Bột sữa", 1, 2 }
                });

            migrationBuilder.InsertData(
                table: "IngredientSuppliers",
                columns: new[] { "IngredientSupplierId", "Active", "CurrentPrice", "IngredientId", "IsPrimary", "LeadTimeDays", "MinimumOrderQuantity", "Note", "SupplierId", "UnitId" },
                values: new object[] { 9, true, 120000m, 3, true, 5, 1m, "Trà Lipton", 4, 1 });

            migrationBuilder.InsertData(
                table: "RecipeDetails",
                columns: new[] { "RecipeDetailId", "ChildRecipeId", "IngredientId", "Quantity", "RecipeId", "UnitId" },
                values: new object[,]
                {
                    { 15, null, 11, 100m, 5, 1 },
                    { 16, null, 12, 50m, 5, 1 },
                    { 17, null, 13, 60m, 5, 3 },
                    { 18, null, 11, 100m, 6, 1 },
                    { 19, null, 6, 40m, 6, 1 },
                    { 20, null, 13, 60m, 6, 3 }
                });

            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "RecipeId", "Active", "DrinkId", "EffectiveDate", "Name", "ParentVersionId", "RecipeCode", "SizeId", "Status", "ToppingId", "YieldPercentage" },
                values: new object[,]
                {
                    { 1, true, 1, null, "Recipe CF Sữa", null, "RCP_CF_SUA", 1, "Active", null, 100m },
                    { 2, true, 2, null, "Recipe CF Đen", null, "RCP_CF_DEN", 1, "Active", null, 100m },
                    { 3, true, 3, null, "Recipe Trà sữa", null, "RCP_TS", 1, "Active", null, 100m },
                    { 4, true, 4, null, "Recipe Trà sữa socola", null, "RCP_TS_SOCOLA", 1, "Active", null, 100m }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 100, 1 }
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
                columns: new[] { "StaffBankId", "AccountHolderName", "AccountNumber", "BankName", "IsPrimary", "StaffId" },
                values: new object[,]
                {
                    { 1, null, "123456789", "Vietcombank", false, 1 },
                    { 2, null, "987654321", "ACB", false, 2 },
                    { 3, null, "456123789", "Techcombank", false, 3 }
                });

            migrationBuilder.InsertData(
                table: "StaffPhones",
                columns: new[] { "StaffPhoneId", "IsDefault", "Phone", "StaffId" },
                values: new object[,]
                {
                    { 1, true, "0901000101", 1 },
                    { 2, true, "0901000102", 2 },
                    { 3, true, "0901000103", 3 },
                    { 4, true, "0901000104", 4 },
                    { 5, true, "0901000105", 5 },
                    { 6, true, "0901000106", 6 }
                });

            migrationBuilder.InsertData(
                table: "StaffScopes",
                columns: new[] { "StaffScopeId", "ScopeRefId", "ScopeTypeId", "StaffId" },
                values: new object[,]
                {
                    { 1, 1, 1, 1 },
                    { 2, 1, 1, 6 },
                    { 3, 79, 2, 2 },
                    { 4, 1, 5, 3 },
                    { 5, 1, 5, 4 },
                    { 6, 1, 5, 5 }
                });

            migrationBuilder.InsertData(
                table: "StaffShifts",
                columns: new[] { "StaffShiftId", "ActualCheckIn", "ActualCheckOut", "CustomEndTime", "CustomStartTime", "IsAdHoc", "PayrollHours", "ShiftId", "StaffId", "StatusId", "WorkDate" },
                values: new object[,]
                {
                    { 1, null, null, null, null, false, null, 1, 4, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, null, null, null, null, false, null, 2, 5, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, null, null, null, null, false, null, 4, 6, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
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
                columns: new[] { "StoreInventoryId", "AvailableQty", "IngredientId", "LastUpdated", "MaxNegativeQty", "RecipeId", "StoreId" },
                values: new object[,]
                {
                    { 1, 100m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 1 },
                    { 2, 50m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 1 },
                    { 3, 80m, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 2 },
                    { 4, 60m, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 3 }
                });

            migrationBuilder.InsertData(
                table: "SupplierContactPhones",
                columns: new[] { "SupplierContactPhoneId", "Description", "IsPrimary", "PhoneNumber", "SupplierContactId" },
                values: new object[,]
                {
                    { 1, "Số chính", true, "0901111111", 1 },
                    { 2, "Sales", true, "0902222222", 2 },
                    { 3, "Owner", true, "0903333333", 3 },
                    { 4, "Director", true, "0904444444", 4 },
                    { 5, "Manager", true, "0905555555", 5 }
                });

            migrationBuilder.InsertData(
                table: "UnitConversions",
                columns: new[] { "UnitConversionId", "FromQuantity", "FromUnitId", "IngredientId", "ToQuantity", "ToUnitId" },
                values: new object[,]
                {
                    { 1, 1m, 2, 1, 1000m, 1 },
                    { 2, 1m, 2, 3, 1000m, 1 },
                    { 3, 1m, 2, 4, 1000m, 1 },
                    { 4, 1m, 2, 5, 1000m, 1 },
                    { 5, 1m, 2, 6, 1000m, 1 },
                    { 6, 1m, 2, 7, 1000m, 1 },
                    { 7, 1m, 2, 9, 1000m, 1 },
                    { 8, 1m, 2, 11, 1000m, 1 },
                    { 9, 1m, 2, 12, 1000m, 1 },
                    { 20, 1m, 4, 2, 1000m, 3 },
                    { 21, 1m, 4, 8, 1000m, 3 },
                    { 22, 1m, 4, 10, 1000m, 3 },
                    { 23, 1m, 4, 13, 1000m, 3 },
                    { 30, 1m, 5, 2, 29.5735m, 3 },
                    { 31, 1m, 5, 8, 29.5735m, 3 },
                    { 32, 1m, 5, 10, 29.5735m, 3 },
                    { 40, 1m, 6, 2, 240m, 3 },
                    { 41, 1m, 6, 8, 240m, 3 },
                    { 42, 1m, 6, 10, 240m, 3 },
                    { 50, 1m, 7, 2, 15m, 3 },
                    { 60, 1m, 8, 2, 5m, 3 },
                    { 70, 1m, 10, 8, 750m, 3 },
                    { 71, 1m, 11, 2, 300m, 3 },
                    { 72, 1m, 11, 13, 500m, 3 }
                });

            migrationBuilder.InsertData(
                table: "IngredientSupplierPriceHistories",
                columns: new[] { "IngredientSupplierPriceHistoryId", "EffectiveDate", "IngredientSupplierId", "IsCurrent", "Note", "Price" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, true, "Giá ban đầu", 22000m },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "Giá ban đầu", 27000m },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "Giá ban đầu", 140000m }
                });

            migrationBuilder.InsertData(
                table: "RecipeDetails",
                columns: new[] { "RecipeDetailId", "ChildRecipeId", "IngredientId", "Quantity", "RecipeId", "UnitId" },
                values: new object[,]
                {
                    { 1, null, 1, 50m, 1, 3 },
                    { 2, null, 2, 30m, 1, 3 },
                    { 3, null, 7, 100m, 1, 3 },
                    { 4, null, 1, 60m, 2, 3 },
                    { 5, null, 7, 100m, 2, 3 },
                    { 6, null, 3, 80m, 3, 3 },
                    { 7, null, 4, 40m, 3, 3 },
                    { 8, null, 6, 20m, 3, 3 },
                    { 9, null, 7, 100m, 3, 3 },
                    { 10, null, 3, 70m, 4, 3 },
                    { 11, null, 4, 40m, 4, 3 },
                    { 12, null, 5, 20m, 4, 3 },
                    { 13, null, 6, 20m, 4, 3 },
                    { 14, null, 7, 100m, 4, 3 },
                    { 21, 5, null, 1m, 3, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPermissionOverrides_AccountId_PermissionId",
                table: "AccountPermissionOverrides",
                columns: new[] { "AccountId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountPermissionOverrides_PermissionId",
                table: "AccountPermissionOverrides",
                column: "PermissionId");

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
                name: "IX_AttendanceLogs_StoreId",
                table: "AttendanceLogs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_UserId",
                table: "AttendanceLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RecordId",
                table: "AuditLogs",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TableName",
                table: "AuditLogs",
                column: "TableName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TableName_Action",
                table: "AuditLogs",
                columns: new[] { "TableName", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TableName_RecordId",
                table: "AuditLogs",
                columns: new[] { "TableName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

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
                name: "IX_CustomerAddresses_DistrictId",
                table: "CustomerAddresses",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_ProvinceId",
                table: "CustomerAddresses",
                column: "ProvinceId");

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
                name: "IX_Customers_AccountId",
                table: "Customers",
                column: "AccountId",
                unique: true,
                filter: "[AccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_MemberLevelId",
                table: "Customers",
                column: "MemberLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_CustomerId_VoucherId",
                table: "CustomerVouchers",
                columns: new[] { "CustomerId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVouchers_VoucherId",
                table: "CustomerVouchers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceId_Name",
                table: "Districts",
                columns: new[] { "ProvinceId", "Name" },
                unique: true,
                filter: "[ProvinceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkCategories_CategoryCode",
                table: "DrinkCategories",
                column: "CategoryCode",
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
                name: "IX_DrinkImages_DrinkId_IsDefault",
                table: "DrinkImages",
                columns: new[] { "DrinkId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_CategoryId_ProductTypeId",
                table: "Drinks",
                columns: new[] { "CategoryId", "ProductTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Drinks_DrinkCode",
                table: "Drinks",
                column: "DrinkCode",
                unique: true);

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
                name: "IX_Ingredients_BaseUnitId",
                table: "Ingredients",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Code",
                table: "Ingredients",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_IngredientSupplierId",
                table: "IngredientSupplierPriceHistories",
                column: "IngredientSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_IngredientSupplierId_EffectiveDate",
                table: "IngredientSupplierPriceHistories",
                columns: new[] { "IngredientSupplierId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_Active",
                table: "IngredientSuppliers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_IngredientId",
                table: "IngredientSuppliers",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_IngredientId_SupplierId",
                table: "IngredientSuppliers",
                columns: new[] { "IngredientId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_SupplierId",
                table: "IngredientSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_UnitId",
                table: "IngredientSuppliers",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_InventoryCostLayerId",
                table: "InventoryCostAllocations",
                column: "InventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_InventoryDocumentDetailId",
                table: "InventoryCostAllocations",
                column: "InventoryDocumentDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_InventoryDocumentDetailId_InventoryCostLayerId",
                table: "InventoryCostAllocations",
                columns: new[] { "InventoryDocumentDetailId", "InventoryCostLayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_CreatedAt",
                table: "InventoryCostLayers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_IngredientId",
                table: "InventoryCostLayers",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_StoreId",
                table: "InventoryCostLayers",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_StoreId_IngredientId",
                table: "InventoryCostLayers",
                columns: new[] { "StoreId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_StoreId_IngredientId_RemainingQuantity",
                table: "InventoryCostLayers",
                columns: new[] { "StoreId", "IngredientId", "RemainingQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDebts_InventoryDocumentId",
                table: "InventoryDebts",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDebts_PaidAmount",
                table: "InventoryDebts",
                column: "PaidAmount");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDebts_PartnerType_PartnerId",
                table: "InventoryDebts",
                columns: new[] { "PartnerType", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_IngredientId",
                table: "InventoryDocumentDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_InventoryDocumentId",
                table: "InventoryDocumentDetails",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_InventoryDocumentId_IngredientId",
                table: "InventoryDocumentDetails",
                columns: new[] { "InventoryDocumentId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentDetails_UnitId",
                table: "InventoryDocumentDetails",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_Code",
                table: "InventoryDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_DocumentDate",
                table: "InventoryDocuments",
                column: "DocumentDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_RequestKey",
                table: "InventoryDocuments",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StaffId",
                table: "InventoryDocuments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_Status",
                table: "InventoryDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StoreId_DocumentDate",
                table: "InventoryDocuments",
                columns: new[] { "StoreId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StoreId_Purpose",
                table: "InventoryDocuments",
                columns: new[] { "StoreId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_StoreId_Type_Status",
                table: "InventoryDocuments",
                columns: new[] { "StoreId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_SupplierId",
                table: "InventoryDocuments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocuments_Type",
                table: "InventoryDocuments",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentSnapshotDetails_InventoryDocumentSnapshotId",
                table: "InventoryDocumentSnapshotDetails",
                column: "InventoryDocumentSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentSnapshots_Code",
                table: "InventoryDocumentSnapshots",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentSnapshots_CreatedAt",
                table: "InventoryDocumentSnapshots",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentSnapshots_DocumentDate",
                table: "InventoryDocumentSnapshots",
                column: "DocumentDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentSnapshots_InventoryDocumentId",
                table: "InventoryDocumentSnapshots",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CreatedAt",
                table: "InventoryTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryDocumentId",
                table: "InventoryTransactions",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryTransferId",
                table: "InventoryTransactions",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceOrderId",
                table: "InventoryTransactions",
                column: "ReferenceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StockStatus",
                table: "InventoryTransactions",
                column: "StockStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StoreInventoryId",
                table: "InventoryTransactions",
                column: "StoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_StoreInventoryId_CreatedAt",
                table: "InventoryTransactions",
                columns: new[] { "StoreInventoryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_Type",
                table: "InventoryTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_Type_CreatedAt",
                table: "InventoryTransactions",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_IngredientId",
                table: "InventoryTransferDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_IngredientId_InventoryTransferId",
                table: "InventoryTransferDetails",
                columns: new[] { "IngredientId", "InventoryTransferId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_InventoryTransferId_IngredientId",
                table: "InventoryTransferDetails",
                columns: new[] { "InventoryTransferId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_UnitId",
                table: "InventoryTransferDetails",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_CancelledByStaffId",
                table: "InventoryTransfers",
                column: "CancelledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_Code",
                table: "InventoryTransfers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ConfirmedByStaffId",
                table: "InventoryTransfers",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_CreatedAt",
                table: "InventoryTransfers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_CreatedByStaffId_CreatedAt",
                table: "InventoryTransfers",
                columns: new[] { "CreatedByStaffId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_DocumentDate",
                table: "InventoryTransfers",
                column: "DocumentDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_FromStoreId_Status",
                table: "InventoryTransfers",
                columns: new[] { "FromStoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_FromStoreId_ToStoreId",
                table: "InventoryTransfers",
                columns: new[] { "FromStoreId", "ToStoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_RequestKey",
                table: "InventoryTransfers",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_Status",
                table: "InventoryTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ToStoreId_Status",
                table: "InventoryTransfers",
                columns: new[] { "ToStoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_CashierId",
                table: "InvoiceAuditLogs",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_CreatedAt",
                table: "InvoiceAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_OrderId",
                table: "InvoiceAuditLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_SupervisorId",
                table: "InvoiceAuditLogs",
                column: "SupervisorId");

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
                name: "IX_Orders_ClientOrderId_Unique",
                table: "Orders",
                column: "ClientOrderId",
                unique: true,
                filter: "[ClientOrderId] IS NOT NULL");

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
                name: "IX_Orders_PaymentStatusId",
                table: "Orders",
                column: "PaymentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StaffId",
                table: "Orders",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId",
                table: "Orders",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WorkShiftId",
                table: "Orders",
                column: "WorkShiftId");

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
                name: "IX_OtpChallenges_ActionType_TargetType_TargetId_StoreId",
                table: "OtpChallenges",
                columns: new[] { "ActionType", "TargetType", "TargetId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_ApproverStaffId_Status_ExpiresAt",
                table: "OtpChallenges",
                columns: new[] { "ApproverStaffId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_CreatedAt",
                table: "OtpChallenges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_PublicId",
                table: "OtpChallenges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_RequestedByStaffId",
                table: "OtpChallenges",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_StoreId_Status_ExpiresAt",
                table: "OtpChallenges",
                columns: new[] { "StoreId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_WorkShiftId",
                table: "OtpChallenges",
                column: "WorkShiftId");

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
                name: "IX_PermissionGroups_Code",
                table: "PermissionGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionGroups_Name",
                table: "PermissionGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionGroupId_Action",
                table: "Permissions",
                columns: new[] { "PermissionGroupId", "Action" },
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
                name: "IX_PosTerminals_StoreId",
                table: "PosTerminals",
                column: "StoreId");

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
                name: "IX_RatingImages_PublicId",
                table: "RatingImages",
                column: "PublicId",
                unique: true);

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
                name: "IX_RecipeDetails_UnitId",
                table: "RecipeDetails",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DrinkId_SizeId",
                table: "Recipes",
                columns: new[] { "DrinkId", "SizeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ParentVersionId",
                table: "Recipes",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_RecipeCode",
                table: "Recipes",
                column: "RecipeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SizeId",
                table: "Recipes",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ToppingId",
                table: "Recipes",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_ActionName",
                table: "RequestDeduplications",
                column: "ActionName");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_ActionName_StaffId",
                table: "RequestDeduplications",
                columns: new[] { "ActionName", "StaffId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_ExpiredAt",
                table: "RequestDeduplications",
                column: "ExpiredAt");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_RequestKey_ActionName_StaffId",
                table: "RequestDeduplications",
                columns: new[] { "RequestKey", "ActionName", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_StaffId",
                table: "RequestDeduplications",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_Status",
                table: "RequestDeduplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_Status_ExpiredAt",
                table: "RequestDeduplications",
                columns: new[] { "Status", "ExpiredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

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
                name: "IX_Sizes_SizeCode",
                table: "Sizes",
                column: "SizeCode",
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
                name: "IX_StaffDependents_StaffId",
                table: "StaffDependents",
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
                name: "IX_Staffs_CCCD",
                table: "Staffs",
                column: "CCCD",
                unique: true,
                filter: "[CCCD] IS NOT NULL AND [CCCD] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StoreId",
                table: "Staffs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_TaxCode",
                table: "Staffs",
                column: "TaxCode",
                unique: true,
                filter: "[TaxCode] IS NOT NULL AND [TaxCode] <> ''");

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
                unique: true,
                filter: "[ShiftId] IS NOT NULL");

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
                name: "IX_StockTakeDetails_IngredientId",
                table: "StockTakeDetails",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeDetails_StockTakeSessionId",
                table: "StockTakeDetails",
                column: "StockTakeSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeDetails_StockTakeSessionId_IngredientId",
                table: "StockTakeDetails",
                columns: new[] { "StockTakeSessionId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeSessions_Code",
                table: "StockTakeSessions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeSessions_CreatedAt",
                table: "StockTakeSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeSessions_StaffId",
                table: "StockTakeSessions",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeSessions_StoreId",
                table: "StockTakeSessions",
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
                name: "IX_StoreInventories_RecipeId",
                table: "StoreInventories",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_StoreId",
                table: "StoreInventories",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Recipe",
                table: "StoreInventories",
                columns: new[] { "StoreId", "RecipeId" },
                unique: true,
                filter: "[RecipeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventorySnapshots_IngredientId",
                table: "StoreInventorySnapshots",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventorySnapshots_StoreId",
                table: "StoreInventorySnapshots",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventorySnapshots_StoreId_IngredientId",
                table: "StoreInventorySnapshots",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventorySnapshots_UpdatedAt",
                table: "StoreInventorySnapshots",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StoreIPs_StoreId",
                table: "StoreIPs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_DistrictId",
                table: "Stores",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ProvinceId",
                table: "Stores",
                column: "ProvinceId");

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
                name: "IX_SupplierBankAccounts_SupplierId",
                table: "SupplierBankAccounts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBankAccounts_SupplierId_AccountNumber",
                table: "SupplierBankAccounts",
                columns: new[] { "SupplierId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContactPhones_SupplierContactId",
                table: "SupplierContactPhones",
                column: "SupplierContactId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContactPhones_SupplierContactId_PhoneNumber",
                table: "SupplierContactPhones",
                columns: new[] { "SupplierContactId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_Email",
                table: "SupplierContacts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId",
                table: "SupplierContacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPhones_SupplierId",
                table: "SupplierPhones",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPhones_SupplierId_PhoneNumber",
                table: "SupplierPhones",
                columns: new[] { "SupplierId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Active",
                table: "Suppliers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Status",
                table: "Suppliers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TaxCode",
                table: "Suppliers",
                column: "TaxCode");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_SettingKey",
                table: "SystemSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Toppings_Name",
                table: "Toppings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Toppings_ToppingCode",
                table: "Toppings",
                column: "ToppingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_FromUnitId",
                table: "UnitConversions",
                column: "FromUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_IngredientId",
                table: "UnitConversions",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_IngredientId_FromUnitId_ToUnitId",
                table: "UnitConversions",
                columns: new[] { "IngredientId", "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_ToUnitId",
                table: "UnitConversions",
                column: "ToUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Type",
                table: "Units",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Units_UnitCode",
                table: "Units",
                column: "UnitCode",
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
                name: "IX_Wards_DistrictId_Name",
                table: "Wards",
                columns: new[] { "DistrictId", "Name" },
                unique: true,
                filter: "[DistrictId] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_ExceptionClosedByStaffId",
                table: "WorkShifts",
                column: "ExceptionClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_PosTerminalId",
                table: "WorkShifts",
                column: "PosTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_StoreId",
                table: "WorkShifts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_StoreId_RequiresReconciliation",
                table: "WorkShifts",
                columns: new[] { "StoreId", "RequiresReconciliation" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_UserId",
                table: "WorkShifts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPermissionOverrides");

            migrationBuilder.DropTable(
                name: "AccountRoles");

            migrationBuilder.DropTable(
                name: "AttendanceLogs");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CashFlowDto");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerBanks");

            migrationBuilder.DropTable(
                name: "CustomerPhones");

            migrationBuilder.DropTable(
                name: "CustomerVouchers");

            migrationBuilder.DropTable(
                name: "DashboardSummaryDto");

            migrationBuilder.DropTable(
                name: "DrinkDefaultToppings");

            migrationBuilder.DropTable(
                name: "DrinkImages");

            migrationBuilder.DropTable(
                name: "DrinkSizes");

            migrationBuilder.DropTable(
                name: "DrinkToppings");

            migrationBuilder.DropTable(
                name: "IngredientSupplierPriceHistories");

            migrationBuilder.DropTable(
                name: "InventoryCostAllocations");

            migrationBuilder.DropTable(
                name: "InventoryDebts");

            migrationBuilder.DropTable(
                name: "InventoryDocumentSnapshotDetails");

            migrationBuilder.DropTable(
                name: "InventoryDto");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "InventoryTransferDetails");

            migrationBuilder.DropTable(
                name: "InvoiceAuditLogs");

            migrationBuilder.DropTable(
                name: "OrderToppings");

            migrationBuilder.DropTable(
                name: "OrderVouchers");

            migrationBuilder.DropTable(
                name: "OtpChallenges");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropTable(
                name: "PaymentMethodDto");

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
                name: "RequestDeduplications");

            migrationBuilder.DropTable(
                name: "RevenueByStoreDto");

            migrationBuilder.DropTable(
                name: "RevenueDto");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "StaffAddresses");

            migrationBuilder.DropTable(
                name: "StaffBanks");

            migrationBuilder.DropTable(
                name: "StaffDependents");

            migrationBuilder.DropTable(
                name: "StaffPerformanceDto");

            migrationBuilder.DropTable(
                name: "StaffPhones");

            migrationBuilder.DropTable(
                name: "StaffScopes");

            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "StockTakeDetails");

            migrationBuilder.DropTable(
                name: "StoreDrinks");

            migrationBuilder.DropTable(
                name: "StoreInventorySnapshots");

            migrationBuilder.DropTable(
                name: "StoreIPs");

            migrationBuilder.DropTable(
                name: "StoreToppings");

            migrationBuilder.DropTable(
                name: "SupplierBankAccounts");

            migrationBuilder.DropTable(
                name: "SupplierContactPhones");

            migrationBuilder.DropTable(
                name: "SupplierPhones");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TopDrinkDto");

            migrationBuilder.DropTable(
                name: "TopToppingDto");

            migrationBuilder.DropTable(
                name: "TransactionLogs");

            migrationBuilder.DropTable(
                name: "UnitConversions");

            migrationBuilder.DropTable(
                name: "VoucherUsages");

            migrationBuilder.DropTable(
                name: "WasteDto");

            migrationBuilder.DropTable(
                name: "WheelSpins");

            migrationBuilder.DropTable(
                name: "IngredientSuppliers");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");

            migrationBuilder.DropTable(
                name: "InventoryDocumentDetails");

            migrationBuilder.DropTable(
                name: "InventoryDocumentSnapshots");

            migrationBuilder.DropTable(
                name: "StoreInventories");

            migrationBuilder.DropTable(
                name: "InventoryTransfers");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PointTransactionTypes");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ScopeTypes");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "StaffShiftStatuses");

            migrationBuilder.DropTable(
                name: "StockTakeSessions");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "WheelPrizes");

            migrationBuilder.DropTable(
                name: "InventoryDocuments");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "PermissionGroups");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "WheelConfigs");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Drinks");

            migrationBuilder.DropTable(
                name: "Sizes");

            migrationBuilder.DropTable(
                name: "Toppings");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "OrderStatuses");

            migrationBuilder.DropTable(
                name: "OrderTypes");

            migrationBuilder.DropTable(
                name: "PaymentStatuses");

            migrationBuilder.DropTable(
                name: "WorkShifts");

            migrationBuilder.DropTable(
                name: "DrinkCategories");

            migrationBuilder.DropTable(
                name: "ProductTypes");

            migrationBuilder.DropTable(
                name: "MemberLevels");

            migrationBuilder.DropTable(
                name: "PosTerminals");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "Wards");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
