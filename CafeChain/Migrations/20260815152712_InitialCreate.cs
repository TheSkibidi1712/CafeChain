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
                name: "DocumentNumberCounters",
                columns: table => new
                {
                    DocumentNumberCounterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CounterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateKey = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNumberCounters", x => x.DocumentNumberCounterId);
                    table.CheckConstraint("CK_DocumentNumberCounter_Value", "[LastValue] > 0");
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
                name: "ImportSessions",
                columns: table => new
                {
                    ImportSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    SourceFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SourceMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UploadedByAccountId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AnalysisVersion = table.Column<int>(type: "int", nullable: false),
                    PreviewVersion = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExtractionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "ai-import-extraction-v2"),
                    TotalGroups = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    WarningRows = table.Column<int>(type: "int", nullable: false),
                    ErrorRows = table.Column<int>(type: "int", nullable: false),
                    ReviewRows = table.Column<int>(type: "int", nullable: false),
                    SkippedRows = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnalysisWarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedOcr = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveOcr = table.Column<bool>(type: "bit", nullable: false),
                    OcrConfigVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.ImportSessionId);
                    table.CheckConstraint("CK_ImportSessions_Status", "[Status] IN ('UPLOADED','ANALYZING','VALIDATING','READY_TO_PREVIEW','IMPORTING','COMPLETED','FAILED','CANCELLED','EXPIRED')");
                });

            migrationBuilder.CreateTable(
                name: "IntelligencePilotRuns",
                columns: table => new
                {
                    IntelligencePilotRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RunMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntelligencePilotRuns", x => x.IntelligencePilotRunId);
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
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingLeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDeduplications", x => x.RequestDeduplicationId);
                    table.CheckConstraint("CK_RequestDeduplication_ExpiredAt", "[ExpiredAt] > [CreatedAt]");
                    table.CheckConstraint("CK_RequestDeduplication_Status", "[Status] IN ('PROCESSING', 'SUCCESS', 'FAILED', 'EXPIRED')");
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
                    SizeType = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
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
                    TaxCode = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierId);
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
                name: "TransactionLogs",
                columns: table => new
                {
                    TransactionLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    Code = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                name: "ImportAudits",
                columns: table => new
                {
                    ImportAuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportSessionId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StatusBefore = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StatusAfter = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExtractionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "ai-import-extraction-v2"),
                    PreviewVersion = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResultSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExtractionMode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OcrUsed = table.Column<bool>(type: "bit", nullable: false),
                    OcrPageCount = table.Column<int>(type: "int", nullable: false),
                    OcrProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OcrProviderVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OcrExtractionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OcrConfidenceSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiChunkCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAudits", x => x.ImportAuditId);
                    table.ForeignKey(
                        name: "FK_ImportAudits_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "ImportSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportSourceDocuments",
                columns: table => new
                {
                    ImportSourceDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportSessionId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    SourceFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SourceMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSourceDocuments", x => x.ImportSourceDocumentId);
                    table.CheckConstraint("CK_ImportSourceDocuments_Status", "[Status] IN ('PROCESSING','READY','FAILED','REMOVED')");
                    table.ForeignKey(
                        name: "FK_ImportSourceDocuments_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "ImportSessionId",
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
                name: "SupplierContacts",
                columns: table => new
                {
                    SupplierContactId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                name: "PreparedItems",
                columns: table => new
                {
                    PreparedItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedItems", x => x.PreparedItemId);
                    table.ForeignKey(
                        name: "FK_PreparedItems_Units_BaseUnitId",
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
                name: "Wards",
                columns: table => new
                {
                    WardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nchar(5)", fixedLength: true, maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.WardId);
                    table.ForeignKey(
                        name: "FK_Wards_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportGroups",
                columns: table => new
                {
                    ImportGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportSessionId = table.Column<int>(type: "int", nullable: false),
                    ImportSourceDocumentId = table.Column<int>(type: "int", nullable: true),
                    SheetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RegionAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceLocatorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtractionMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HeaderRow = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DependencyOrder = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LayoutConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportGroups", x => x.ImportGroupId);
                    table.ForeignKey(
                        name: "FK_ImportGroups_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "ImportSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportGroups_ImportSourceDocuments_ImportSourceDocumentId",
                        column: x => x.ImportSourceDocumentId,
                        principalTable: "ImportSourceDocuments",
                        principalColumn: "ImportSourceDocumentId");
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
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "IngredientSuppliers",
                columns: table => new
                {
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PackageQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumOrderPackageCount = table.Column<int>(type: "int", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AllowsLoosePurchase = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CurrentProcurementUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LooseProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    LoosePriceMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "INDEPENDENT"),
                    LooseMinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    LooseQuantityStep = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSuppliers", x => x.IngredientSupplierId);
                    table.CheckConstraint("CK_IngredientSupplier_CurrentPrice", "[CurrentPrice] >= 0");
                    table.CheckConstraint("CK_IngredientSupplier_LeadTime", "[LeadTimeDays] IS NULL OR [LeadTimeDays] >= 0");
                    table.CheckConstraint("CK_IngredientSupplier_LooseMinimumOrderQuantity", "[LooseMinimumOrderQuantity] IS NULL OR [LooseMinimumOrderQuantity] >= 0");
                    table.CheckConstraint("CK_IngredientSupplier_LoosePriceMode", "[LoosePriceMode] IN ('DERIVED', 'INDEPENDENT')");
                    table.CheckConstraint("CK_IngredientSupplier_LoosePurchase", "[AllowsLoosePurchase] = 0 OR ([CurrentProcurementUnitPrice] IS NOT NULL AND [CurrentProcurementUnitPrice] > 0 AND [LooseProcurementUnitId] IS NOT NULL)");
                    table.CheckConstraint("CK_IngredientSupplier_LooseQuantityStep", "[LooseQuantityStep] IS NULL OR [LooseQuantityStep] > 0");
                    table.CheckConstraint("CK_IngredientSupplier_MOQ", "[MinimumOrderPackageCount] IS NULL OR [MinimumOrderPackageCount] > 0");
                    table.CheckConstraint("CK_IngredientSupplier_PackageQuantity", "[PackageQuantity] IS NULL OR [PackageQuantity] > 0");
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
                        name: "FK_IngredientSuppliers_Units_LooseProcurementUnitId",
                        column: x => x.LooseProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
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
                name: "InventoryItemSourceCapabilities",
                columns: table => new
                {
                    InventoryItemSourceCapabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    CanProduce = table.Column<bool>(type: "bit", nullable: false),
                    CanPurchase = table.Column<bool>(type: "bit", nullable: false),
                    CanTransfer = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemSourceCapabilities", x => x.InventoryItemSourceCapabilityId);
                    table.CheckConstraint("CK_InventoryItemSourceCapabilities_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_InventoryItemSourceCapabilities_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventoryItemSourceCapabilities_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItemSourceCapabilities_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
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
                    YieldPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 100m),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentVersionId = table.Column<int>(type: "int", nullable: true),
                    DrinkId = table.Column<int>(type: "int", nullable: true),
                    SizeId = table.Column<int>(type: "int", nullable: true),
                    ToppingId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    OutputQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    OutputUnitId = table.Column<int>(type: "int", nullable: true),
                    YieldVarianceTolerancePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.CheckConstraint("CK_Recipes_OutputQuantity_Positive", "[OutputQuantity] IS NULL OR [OutputQuantity] > 0");
                    table.CheckConstraint("CK_Recipes_PreparedItemOutput_AllOrNone", "([PreparedItemId] IS NULL AND [OutputQuantity] IS NULL AND [OutputUnitId] IS NULL)\r\n                    OR ([PreparedItemId] IS NOT NULL AND [OutputQuantity] IS NOT NULL AND [OutputQuantity] > 0 AND [OutputUnitId] IS NOT NULL)");
                    table.CheckConstraint("CK_Recipes_YieldVarianceTolerance", "[YieldVarianceTolerancePercent] IS NULL OR ([YieldVarianceTolerancePercent] >= 0 AND [YieldVarianceTolerancePercent] <= 100)");
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
                        name: "FK_Recipes_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_Recipes_Units_OutputUnitId",
                        column: x => x.OutputUnitId,
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
                name: "CustomerAddresses",
                columns: table => new
                {
                    CustomerAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WardId = table.Column<int>(type: "int", nullable: true),
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
                    ProvinceId = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreId);
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
                name: "ImportItems",
                columns: table => new
                {
                    ImportItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportGroupId = table.Column<int>(type: "int", nullable: false),
                    SourceRow = table.Column<int>(type: "int", nullable: false),
                    RawDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceTraceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceLocatorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceSnippet = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceIssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    AiConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    OcrConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    LayoutConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    FieldEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    WarningsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    ManualReviewConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ManualReviewConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManualReviewConfirmedByAccountId = table.Column<int>(type: "int", nullable: true),
                    ManualReviewPayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SupplierDuplicateWarningId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuplicateOverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImportedEntityId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportItems", x => x.ImportItemId);
                    table.CheckConstraint("CK_ImportItems_Action", "[Action] IN ('CREATE','SKIP')");
                    table.CheckConstraint("CK_ImportItems_Status", "[Status] IN ('VALID','WARNING','ERROR','REVIEW_REQUIRED','SKIPPED','IMPORTED')");
                    table.ForeignKey(
                        name: "FK_ImportItems_ImportGroups_ImportGroupId",
                        column: x => x.ImportGroupId,
                        principalTable: "ImportGroups",
                        principalColumn: "ImportGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkSizePriceAudits",
                columns: table => new
                {
                    DrinkSizePriceAuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkSizeId = table.Column<int>(type: "int", nullable: false),
                    OldPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CostStatus = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkSizePriceAudits", x => x.DrinkSizePriceAuditId);
                    table.ForeignKey(
                        name: "FK_DrinkSizePriceAudits_DrinkSizes_DrinkSizeId",
                        column: x => x.DrinkSizeId,
                        principalTable: "DrinkSizes",
                        principalColumn: "DrinkSizeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrinkSizeToppingPolicies",
                columns: table => new
                {
                    DrinkSizeToppingPolicyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkSizeId = table.Column<int>(type: "int", nullable: false),
                    ToppingId = table.Column<int>(type: "int", nullable: false),
                    IsDefaultSelected = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PriceTreatment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CostTreatment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    QuantityPerDrink = table.Column<decimal>(type: "decimal(18,5)", nullable: false),
                    QuantityUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "RECIPE_PORTION"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkSizeToppingPolicies", x => x.DrinkSizeToppingPolicyId);
                    table.CheckConstraint("CK_DrinkSizeToppingPolicies_CostTreatment", "[CostTreatment] IN ('INCLUDED_IN_DRINK_RECIPE','ADD_TOPPING_RECIPE_COST','DISPLAY_ONLY')");
                    table.CheckConstraint("CK_DrinkSizeToppingPolicies_PriceTreatment", "[PriceTreatment] IN ('INCLUDED_IN_BASE_PRICE','ADD_TOPPING_PRICE')");
                    table.CheckConstraint("CK_DrinkSizeToppingPolicies_Quantity", "[QuantityPerDrink] > 0");
                    table.CheckConstraint("CK_DrinkSizeToppingPolicies_QuantityUnit", "[QuantityUnit] = 'RECIPE_PORTION'");
                    table.CheckConstraint("CK_DrinkSizeToppingPolicies_RequiredDefault", "[IsRequired] = 0 OR [IsDefaultSelected] = 1");
                    table.ForeignKey(
                        name: "FK_DrinkSizeToppingPolicies_DrinkSizes_DrinkSizeId",
                        column: x => x.DrinkSizeId,
                        principalTable: "DrinkSizes",
                        principalColumn: "DrinkSizeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrinkSizeToppingPolicies_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IngredientSupplierPriceHistories",
                columns: table => new
                {
                    IngredientSupplierPriceHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PackageQuantity = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    PackageUnitId = table.Column<int>(type: "int", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSupplierPriceHistories", x => x.IngredientSupplierPriceHistoryId);
                    table.CheckConstraint("CK_IngredientSupplierPriceHistory_PackageQuantity", "[PackageQuantity] IS NULL OR [PackageQuantity] > 0");
                    table.CheckConstraint("CK_IngredientSupplierPriceHistory_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_IngredientSupplierPriceHistories_IngredientSuppliers_IngredientSupplierId",
                        column: x => x.IngredientSupplierId,
                        principalTable: "IngredientSuppliers",
                        principalColumn: "IngredientSupplierId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientSupplierPriceHistories_Units_PackageUnitId",
                        column: x => x.PackageUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ForecastRuns",
                columns: table => new
                {
                    ForecastRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeriesType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    TrainingFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrainingToExclusive = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorizonDays = table.Column<int>(type: "int", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    Mae = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Wape = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    QualityStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WarningJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InputDataVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastRuns", x => x.ForecastRunId);
                    table.ForeignKey(
                        name: "FK_ForecastRuns_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryWriterModeTransitions",
                columns: table => new
                {
                    TransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    FromMode = table.Column<int>(type: "int", nullable: false),
                    ToMode = table.Column<int>(type: "int", nullable: false),
                    ActorAccountId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReadinessHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReadinessSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryWriterModeTransitions", x => x.TransitionId);
                    table.CheckConstraint("CK_InventoryWriterModeTransition_FromMode", "[FromMode] IN (0, 1, 2)");
                    table.CheckConstraint("CK_InventoryWriterModeTransition_ToMode", "[ToMode] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_InventoryWriterModeTransitions_Accounts_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryWriterModeTransitions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosCatalogStates",
                columns: table => new
                {
                    PosCatalogStateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosCatalogStates", x => x.PosCatalogStateId);
                    table.ForeignKey(
                        name: "FK_PosCatalogStates_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosRecommendationCatalog",
                columns: table => new
                {
                    PosRecommendationCatalogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    TriggerDrinkId = table.Column<int>(type: "int", nullable: false),
                    RecommendedDrinkId = table.Column<int>(type: "int", nullable: false),
                    Support = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Lift = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Margin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosRecommendationCatalog", x => x.PosRecommendationCatalogId);
                    table.ForeignKey(
                        name: "FK_PosRecommendationCatalog_Drinks_RecommendedDrinkId",
                        column: x => x.RecommendedDrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosRecommendationCatalog_Drinks_TriggerDrinkId",
                        column: x => x.TriggerDrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosRecommendationCatalog_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosTerminals",
                columns: table => new
                {
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
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
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    CCCD = table.Column<string>(type: "nchar(12)", fixedLength: true, maxLength: 12, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeStatus = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvatarPublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PosPinHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PosPinFailedAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PosPinLockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    BtpIdentityState = table.Column<int>(type: "int", nullable: true),
                    QuantitySemanticsStatus = table.Column<int>(type: "int", nullable: true),
                    SupersededByStoreInventoryId = table.Column<int>(type: "int", nullable: true),
                    QuantitySemanticsEvidenceType = table.Column<int>(type: "int", nullable: true),
                    QuantitySemanticsEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantitySemanticsReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuantitySemanticsReviewedByAccountId = table.Column<int>(type: "int", nullable: true),
                    AvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    MaxNegativeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    MinStockLevel = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventories", x => x.StoreInventoryId);
                    table.CheckConstraint("CK_StoreInventories_BtpLifecycle", "([IngredientId] IS NOT NULL\r\n                        AND [BtpIdentityState] IS NULL\r\n                        AND [QuantitySemanticsStatus] IS NULL\r\n                        AND [SupersededByStoreInventoryId] IS NULL\r\n                        AND [QuantitySemanticsEvidenceType] IS NULL\r\n                        AND [QuantitySemanticsEvidenceReference] IS NULL\r\n                        AND [QuantitySemanticsReviewedAt] IS NULL\r\n                        AND [QuantitySemanticsReviewedByAccountId] IS NULL)\r\n                    OR ([IngredientId] IS NULL AND (\r\n                        ([BtpIdentityState] = 0 AND [QuantitySemanticsStatus] IS NOT NULL AND [SupersededByStoreInventoryId] IS NULL)\r\n                        OR ([BtpIdentityState] = 1 AND [PreparedItemId] IS NOT NULL AND [QuantitySemanticsStatus] = 1\r\n                            AND [SupersededByStoreInventoryId] IS NULL\r\n                            AND [QuantitySemanticsEvidenceType] IS NOT NULL\r\n                            AND [QuantitySemanticsEvidenceReference] IS NOT NULL\r\n                            AND [QuantitySemanticsReviewedAt] IS NOT NULL\r\n                            AND [QuantitySemanticsReviewedByAccountId] IS NOT NULL)\r\n                        OR ([BtpIdentityState] = 2 AND [QuantitySemanticsStatus] IS NOT NULL AND [SupersededByStoreInventoryId] IS NOT NULL)))");
                    table.CheckConstraint("CK_StoreInventories_NotSelfSuperseded", "[SupersededByStoreInventoryId] IS NULL OR [SupersededByStoreInventoryId] <> [StoreInventoryId]");
                    table.CheckConstraint("CK_StoreInventories_QuantityEvidence", "[QuantitySemanticsStatus] IS NULL\r\n                    OR [QuantitySemanticsStatus] = 0\r\n                    OR ([QuantitySemanticsEvidenceType] IS NOT NULL\r\n                        AND [QuantitySemanticsEvidenceReference] IS NOT NULL\r\n                        AND [QuantitySemanticsReviewedAt] IS NOT NULL\r\n                        AND [QuantitySemanticsReviewedByAccountId] IS NOT NULL)");
                    table.CheckConstraint("CK_StoreInventories_XOR_Item", "([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)\r\n                    OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NULL)\r\n                    OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)\r\n                    OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_StoreInventory_ReservedQty", "[ReservedQty] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreInventories_Accounts_QuantitySemanticsReviewedByAccountId",
                        column: x => x.QuantitySemanticsReviewedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_StoreInventories_SupersededByStoreInventoryId",
                        column: x => x.SupersededByStoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreInventoryWriterConfigurations",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    WriterMode = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HasEverActivatedPreparedItem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventoryWriterConfigurations", x => x.StoreId);
                    table.CheckConstraint("CK_StoreInventoryWriterConfiguration_Mode", "[WriterMode] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_StoreInventoryWriterConfigurations_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreProductionCapabilities",
                columns: table => new
                {
                    StoreProductionCapabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProductionCapabilities", x => x.StoreProductionCapabilityId);
                    table.CheckConstraint("CK_StoreProductionCapabilities_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_StoreProductionCapabilities_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProductionCapabilities_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "SupplierStores",
                columns: table => new
                {
                    SupplierStoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LeadTimeOverrideDays = table.Column<int>(type: "int", nullable: true),
                    DeliverySchedule = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierStores", x => x.SupplierStoreId);
                    table.CheckConstraint("CK_SupplierStore_LeadTimeOverride", "[LeadTimeOverrideDays] IS NULL OR [LeadTimeOverrideDays] >= 0");
                    table.ForeignKey(
                        name: "FK_SupplierStores_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierStores_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrinkSizeToppingPolicyAudits",
                columns: table => new
                {
                    DrinkSizeToppingPolicyAuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrinkSizeToppingPolicyId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OldDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkSizeToppingPolicyAudits", x => x.DrinkSizeToppingPolicyAuditId);
                    table.ForeignKey(
                        name: "FK_DrinkSizeToppingPolicyAudits_DrinkSizeToppingPolicies_DrinkSizeToppingPolicyId",
                        column: x => x.DrinkSizeToppingPolicyId,
                        principalTable: "DrinkSizeToppingPolicies",
                        principalColumn: "DrinkSizeToppingPolicyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForecastPoints",
                columns: table => new
                {
                    ForecastPointId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ForecastRunId = table.Column<long>(type: "bigint", nullable: false),
                    ForecastDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PointForecast = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastPoints", x => x.ForecastPointId);
                    table.ForeignKey(
                        name: "FK_ForecastPoints_ForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "ForecastRuns",
                        principalColumn: "ForecastRunId",
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
                name: "IcePolicies",
                columns: table => new
                {
                    IcePolicyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    DisplayUnitId = table.Column<int>(type: "int", nullable: false),
                    SuggestedDailyQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SuggestedShiftQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AllowSupplementalIssue = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AllowSameDayCarryOver = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireVarianceApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    VarianceApprovalQuantityThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VarianceApprovalPercentThreshold = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcePolicies", x => x.IcePolicyId);
                    table.CheckConstraint("CK_IcePolicies_SuggestedQuantity", "[SuggestedDailyQuantity] >= 0 AND [SuggestedShiftQuantity] >= 0");
                    table.CheckConstraint("CK_IcePolicies_VarianceThreshold", "[VarianceApprovalQuantityThreshold] >= 0 AND [VarianceApprovalPercentThreshold] >= 0");
                    table.ForeignKey(
                        name: "FK_IcePolicies_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Staffs_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcePolicies_Units_DisplayUnitId",
                        column: x => x.DisplayUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryConsolidationRuns",
                columns: table => new
                {
                    InventoryConsolidationRunId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ManifestVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QueryContractVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ManifestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DryRunHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EnvironmentFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ManifestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ExecutedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DryRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BeforeAvailableTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BeforeReservedTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AfterAvailableTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AfterReservedTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryConsolidationRuns", x => x.InventoryConsolidationRunId);
                    table.CheckConstraint("CK_InventoryConsolidationRuns_RunType", "[RunType] IN (1, 2)");
                    table.CheckConstraint("CK_InventoryConsolidationRuns_Status", "[Status] IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationRuns_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationRuns_Staffs_ExecutedByStaffId",
                        column: x => x.ExecutedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationRuns_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationRuns_Stores_StoreId",
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
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    PartnerType = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PartnerId = table.Column<int>(type: "int", nullable: true),
                    PartnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllowNegativeStock = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NegativeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ParentInventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransfers", x => x.InventoryTransferId);
                    table.CheckConstraint("CK_InventoryTransfer_DifferentStore", "[FromStoreId] <> [ToStoreId]");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_InventoryTransfers_ParentInventoryTransferId",
                        column: x => x.ParentInventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "OperationalAnomalies",
                columns: table => new
                {
                    OperationalAnomalyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    DetectionVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AbsoluteDeviation = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PercentageDeviation = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RobustScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WindowFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowToExclusiveUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FeedbackNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FeedbackByStaffId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAnomalies", x => x.OperationalAnomalyId);
                    table.ForeignKey(
                        name: "FK_OperationalAnomalies_Staffs_AcknowledgedByStaffId",
                        column: x => x.AcknowledgedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAnomalies_Staffs_FeedbackByStaffId",
                        column: x => x.FeedbackByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAnomalies_Staffs_ResolvedByStaffId",
                        column: x => x.ResolvedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAnomalies_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationalShifts",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreationSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    SourceScheduleShiftId = table.Column<int>(type: "int", nullable: true),
                    ShiftLeadId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    OpenedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShifts", x => x.OperationalShiftId);
                    table.CheckConstraint("CK_OperationalShifts_CreationSource", "([CreationSource] = 'Manual' AND [SourceScheduleShiftId] IS NULL) OR ([CreationSource] = 'StaffSchedule' AND [SourceScheduleShiftId] IS NOT NULL)");
                    table.CheckConstraint("CK_OperationalShifts_Status", "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
                    table.CheckConstraint("CK_OperationalShifts_TimeRange", "[EndAtUtc] > [StartAtUtc]");
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Shifts_SourceScheduleShiftId",
                        column: x => x.SourceScheduleShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_ClosedByStaffId",
                        column: x => x.ClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_OpenedByStaffId",
                        column: x => x.OpenedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Staffs_ShiftLeadId",
                        column: x => x.ShiftLeadId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShifts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRuns",
                columns: table => new
                {
                    ProductionRunId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    RequestedRunCount = table.Column<decimal>(type: "decimal(18,5)", nullable: false),
                    ContractVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PlannedBatchCount = table.Column<int>(type: "int", nullable: true),
                    ExpectedOutputPerBatchBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: true),
                    ExpectedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: true),
                    OutputBaseUnitId = table.Column<int>(type: "int", nullable: true),
                    YieldVarianceTolerancePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReleasedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByStaffId = table.Column<int>(type: "int", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualRecordedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ActualRecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VarianceApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    VarianceApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VarianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValuationStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalInputCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutputUnitCost = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    ValuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRuns", x => x.ProductionRunId);
                    table.CheckConstraint("CK_ProductionRuns_RequestedRunCount", "[RequestedRunCount] > 0 AND [RequestedRunCount] <= 9999");
                    table.CheckConstraint("CK_ProductionRuns_Status", "[Status] IN (1, 2, 10, 11, 12, 13, 14, 15)");
                    table.CheckConstraint("CK_ProductionRuns_V2BatchContract", "[ContractVersion] = 1 OR ([ContractVersion] = 2 AND [PlannedBatchCount] IS NOT NULL AND [PlannedBatchCount] > 0)");
                    table.CheckConstraint("CK_ProductionRuns_ValuationStatus", "[ValuationStatus] IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_ProductionRuns_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRuns_Staffs_CompletedByStaffId",
                        column: x => x.CompletedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRuns_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRuns_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRuns_Units_OutputBaseUnitId",
                        column: x => x.OutputBaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseAdvices",
                columns: table => new
                {
                    PurchaseAdviceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdviceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NeededByDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseAdvices", x => x.PurchaseAdviceId);
                    table.ForeignKey(
                        name: "FK_PurchaseAdvices_Staffs_CancelledByStaffId",
                        column: x => x.CancelledByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdvices_Staffs_RejectedByStaffId",
                        column: x => x.RejectedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdvices_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdvices_Staffs_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdvices_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderBatches",
                columns: table => new
                {
                    PurchaseOrderBatchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExpectedDeliveryFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDeliveryTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByStaffId = table.Column<int>(type: "int", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderBatches", x => x.PurchaseOrderBatchId);
                    table.CheckConstraint("CK_PurchaseOrderBatches_DeliveryWindow", "[ExpectedDeliveryTo] >= [ExpectedDeliveryFrom]");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatches_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatches_Staffs_CancelledByStaffId",
                        column: x => x.CancelledByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatches_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatches_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleOptimizationProposals",
                columns: table => new
                {
                    ScheduleOptimizationProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConstraintVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ForecastRunId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScoreBreakdownJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ViolationsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOptimizationProposals", x => x.ScheduleOptimizationProposalId);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationProposals_ForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "ForecastRuns",
                        principalColumn: "ForecastRunId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationProposals_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationProposals_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffAddresses",
                columns: table => new
                {
                    StaffAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: true),
                    WardId = table.Column<int>(type: "int", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAddresses", x => x.StaffAddressId);
                    table.ForeignKey(
                        name: "FK_StaffAddresses_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffAddresses_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffAddresses_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "WardId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffAvailabilityExceptions",
                columns: table => new
                {
                    StaffAvailabilityExceptionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAvailabilityExceptions", x => x.StaffAvailabilityExceptionId);
                    table.ForeignKey(
                        name: "FK_StaffAvailabilityExceptions_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffAvailabilityExceptions_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffAvailabilityRules",
                columns: table => new
                {
                    StaffAvailabilityRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAvailabilityRules", x => x.StaffAvailabilityRuleId);
                    table.ForeignKey(
                        name: "FK_StaffAvailabilityRules_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffAvailabilityRules_Staffs_StaffId",
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
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    CustomStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CustomEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "StaffTimeOffs",
                columns: table => new
                {
                    StaffTimeOffId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTimeOffs", x => x.StaffTimeOffId);
                    table.ForeignKey(
                        name: "FK_StaffTimeOffs_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTimeOffs_Staffs_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTimeOffs_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffWorkConstraints",
                columns: table => new
                {
                    StaffWorkConstraintId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetWeeklyHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MaxWeeklyHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MaxDailyHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MinimumRestMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWorkConstraints", x => x.StaffWorkConstraintId);
                    table.ForeignKey(
                        name: "FK_StaffWorkConstraints_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkConstraints_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockAlerts",
                columns: table => new
                {
                    StockAlertId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    RecipeId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    AlertType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentQtySnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ThresholdSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReportedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlerts", x => x.StockAlertId);
                    table.CheckConstraint("CK_StockAlerts_Identity", "\r\n(\r\n  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)\r\n)");
                    table.ForeignKey(
                        name: "FK_StockAlerts_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Staffs_RejectedByStaffId",
                        column: x => x.RejectedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Staffs_ReportedByStaffId",
                        column: x => x.ReportedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockAlerts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
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
                name: "StoreMenuItems",
                columns: table => new
                {
                    StoreMenuItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    DrinkSizeId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    PauseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreMenuItems", x => x.StoreMenuItemId);
                    table.CheckConstraint("CK_StoreMenuItems_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_StoreMenuItems_EffectiveWindow", "[EffectiveToUtc] IS NULL OR [EffectiveFromUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_StoreMenuItems_PriceOverride", "[PriceOverride] IS NULL OR [PriceOverride] >= 0");
                    table.ForeignKey(
                        name: "FK_StoreMenuItems_DrinkSizes_DrinkSizeId",
                        column: x => x.DrinkSizeId,
                        principalTable: "DrinkSizes",
                        principalColumn: "DrinkSizeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreMenuItems_Staffs_PublishedByStaffId",
                        column: x => x.PublishedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreMenuItems_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreStaffingRequirements",
                columns: table => new
                {
                    StoreStaffingRequirementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    MinimumStaff = table.Column<int>(type: "int", nullable: false),
                    TargetStaff = table.Column<int>(type: "int", nullable: false),
                    MaximumStaff = table.Column<int>(type: "int", nullable: false),
                    RequiredRoleId = table.Column<int>(type: "int", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreStaffingRequirements", x => x.StoreStaffingRequirementId);
                    table.ForeignKey(
                        name: "FK_StoreStaffingRequirements_Roles_RequiredRoleId",
                        column: x => x.RequiredRoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreStaffingRequirements_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreStaffingRequirements_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreStaffingRequirements_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDuplicateWarnings",
                columns: table => new
                {
                    SupplierDuplicateWarningId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    WarningFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MatchedSupplierIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchedSignalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedSupplierId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDuplicateWarnings", x => x.SupplierDuplicateWarningId);
                    table.ForeignKey(
                        name: "FK_SupplierDuplicateWarnings_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierDuplicateWarnings_Suppliers_CreatedSupplierId",
                        column: x => x.CreatedSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryConsolidationLines",
                columns: table => new
                {
                    InventoryConsolidationLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryConsolidationRunId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    LineRole = table.Column<int>(type: "int", nullable: false),
                    PreparedItemId = table.Column<int>(type: "int", nullable: false),
                    SourceRecipeId = table.Column<int>(type: "int", nullable: true),
                    BeforeAvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BeforeReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BeforeMinStockLevel = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    BeforeMaxNegativeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    BeforeIdentityState = table.Column<int>(type: "int", nullable: true),
                    BeforeQuantitySemantics = table.Column<int>(type: "int", nullable: true),
                    ApprovedConversionFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ApprovedConversionFromUnitId = table.Column<int>(type: "int", nullable: true),
                    ApprovedConversionToUnitId = table.Column<int>(type: "int", nullable: true),
                    ConvertedAvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AfterAvailableQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AfterReservedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsTargetCreated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryConsolidationLines", x => x.InventoryConsolidationLineId);
                    table.CheckConstraint("CK_InventoryConsolidationLines_LineRole", "[LineRole] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationLines_InventoryConsolidationRuns_InventoryConsolidationRunId",
                        column: x => x.InventoryConsolidationRunId,
                        principalTable: "InventoryConsolidationRuns",
                        principalColumn: "InventoryConsolidationRunId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationLines_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryConsolidationLines_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
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
                    table.CheckConstraint("CK_InventoryDocumentDetail_BaseQuantity", "[BaseQuantity] >= 0");
                    table.CheckConstraint("CK_InventoryDocumentDetail_Quantity", "[Quantity] >= 0");
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
                name: "InventoryDocumentSnapshots",
                columns: table => new
                {
                    InventoryDocumentSnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NegativeApprovalId = table.Column<long>(type: "bigint", nullable: true),
                    BeforeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AfterQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    EffectiveMaxNegativeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostComplete = table.Column<bool>(type: "bit", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_InventoryDocumentSnapshots_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryNegativeApprovals",
                columns: table => new
                {
                    InventoryNegativeApprovalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDocumentId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RequesterStaffId = table.Column<int>(type: "int", nullable: false),
                    ApproverStaffId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScopeAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryNegativeApprovals", x => x.InventoryNegativeApprovalId);
                    table.CheckConstraint("CK_InventoryNegativeApproval_Status", "[Status] IN ('REQUESTED','APPROVED','REJECTED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_InventoryNegativeApprovals_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeApprovals_Staffs_ApproverStaffId",
                        column: x => x.ApproverStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeApprovals_Staffs_RequesterStaffId",
                        column: x => x.RequesterStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeApprovals_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceAllocations",
                columns: table => new
                {
                    IceAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    IcePolicyId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    OpeningCarryQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    InitialIssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SupplementalIssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingCarryQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TheoreticalUsageQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualUsageQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VarianceQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ReservedOutstandingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CostSnapshotStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Missing"),
                    ReservationReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    ReconciliationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReturnReceivedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    OpenedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceAllocations", x => x.IceAllocationId);
                    table.CheckConstraint("CK_IceAllocations_CostStatus", "[CostSnapshotStatus] IN ('Available','Missing')");
                    table.CheckConstraint("CK_IceAllocations_NonNegativeQuantities", "[OpeningCarryQuantity] >= 0 AND [InitialIssuedQuantity] >= 0 AND [SupplementalIssuedQuantity] >= 0 AND [ReturnedQuantity] >= 0 AND [ClosingCarryQuantity] >= 0 AND [TheoreticalUsageQuantity] >= 0 AND [ReservedOutstandingQuantity] >= 0");
                    table.CheckConstraint("CK_IceAllocations_ReturnAudit", "([ReturnedQuantity] = 0) OR ([ReturnedByStaffId] IS NOT NULL AND [ReturnReceivedByStaffId] IS NOT NULL AND [ReturnedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([ReturnCondition]))) > 0)");
                    table.CheckConstraint("CK_IceAllocations_Status", "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceAllocations_IcePolicies_IcePolicyId",
                        column: x => x.IcePolicyId,
                        principalTable: "IcePolicies",
                        principalColumn: "IcePolicyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ClosedByStaffId",
                        column: x => x.ClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_OpenedByStaffId",
                        column: x => x.OpenedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ReturnReceivedByStaffId",
                        column: x => x.ReturnReceivedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_Staffs_ReturnedByStaffId",
                        column: x => x.ReturnedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceAllocations_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunInputActuals",
                columns: table => new
                {
                    ProductionRunInputActualId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    PlannedBaseQuantity = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ActualBaseQuantity = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunInputActuals", x => x.ProductionRunInputActualId);
                    table.CheckConstraint("CK_ProductionRunInputActuals_ItemXor", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_ProductionRunInputActuals_Quantities", "[PlannedBaseQuantity] >= 0 AND [ActualBaseQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunInputActuals_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunOutputs",
                columns: table => new
                {
                    ProductionRunOutputId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    ExpectedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ActualProducedBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    AcceptedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    RejectedOutputBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    VariancePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunOutputs", x => x.ProductionRunOutputId);
                    table.CheckConstraint("CK_ProductionRunOutputs_Quantities", "[ExpectedOutputBase] > 0 AND [ActualProducedBase] >= 0 AND [AcceptedOutputBase] >= 0 AND [RejectedOutputBase] >= 0 AND [AcceptedOutputBase] + [RejectedOutputBase] <= [ActualProducedBase]");
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_Staffs_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunOutputs_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRunTransitions",
                columns: table => new
                {
                    ProductionRunTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRunTransitions", x => x.ProductionRunTransitionId);
                    table.ForeignKey(
                        name: "FK_ProductionRunTransitions_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRunTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseAdviceTransitions",
                columns: table => new
                {
                    PurchaseAdviceTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseAdviceId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseAdviceTransitions", x => x.PurchaseAdviceTransitionId);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceTransitions_PurchaseAdvices_PurchaseAdviceId",
                        column: x => x.PurchaseAdviceId,
                        principalTable: "PurchaseAdvices",
                        principalColumn: "PurchaseAdviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderBatchDocumentRevisions",
                columns: table => new
                {
                    PurchaseOrderBatchDocumentRevisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderBatchId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByStaffId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SentChannel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByStaffId = table.Column<int>(type: "int", nullable: true),
                    SentNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentIdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByRevisionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderBatchDocumentRevisions", x => x.PurchaseOrderBatchDocumentRevisionId);
                    table.CheckConstraint("CK_PurchaseOrderBatchDocumentRevisions_RevisionPositive", "[RevisionNumber] > 0");
                    table.CheckConstraint("CK_PurchaseOrderBatchDocumentRevisions_Status", "[Status] IN ('GENERATED','SENT','SUPERSEDED')");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatchDocumentRevisions_SupersededByRevisionId",
                        column: x => x.SupersededByRevisionId,
                        principalTable: "PurchaseOrderBatchDocumentRevisions",
                        principalColumn: "PurchaseOrderBatchDocumentRevisionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatches_PurchaseOrderBatchId",
                        column: x => x.PurchaseOrderBatchId,
                        principalTable: "PurchaseOrderBatches",
                        principalColumn: "PurchaseOrderBatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchDocumentRevisions_Staffs_GeneratedByStaffId",
                        column: x => x.GeneratedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchDocumentRevisions_Staffs_SentByStaffId",
                        column: x => x.SentByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderBatchLines",
                columns: table => new
                {
                    PurchaseOrderBatchLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderBatchId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false),
                    PackageUnitId = table.Column<int>(type: "int", nullable: true),
                    PackageQuantitySnapshot = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: true),
                    TotalPackageCount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    OrderedPackageCount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    TotalBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitPricePerPackage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPricePerProcurementUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DemandCoveredProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RoundingSurplusProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    PackagePriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderBatchLines", x => x.PurchaseOrderBatchLineId);
                    table.CheckConstraint("CK_PurchaseOrderBatchLines_BasePositive", "[TotalBaseQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseOrderBatchLines_PriceNonNegative", "([PackagePriceSnapshot] IS NULL OR [PackagePriceSnapshot] >= 0) AND [LineTotal] >= 0");
                    table.CheckConstraint("CK_PurchaseOrderBatchLines_PurchaseModeAuthority", "([PurchaseMode] = 'Packaged' AND [PackageQuantitySnapshot] IS NOT NULL AND [PackageQuantitySnapshot] > 0 AND [OrderedPackageCount] IS NOT NULL AND [OrderedPackageCount] > 0 AND [OrderedPackageCount] = FLOOR([OrderedPackageCount]) AND [UnitPricePerPackage] IS NOT NULL AND [UnitPricePerPackage] >= 0 AND [UnitPricePerProcurementUnit] IS NULL) OR ([PurchaseMode] = 'Loose' AND [OrderedPackageCount] IS NULL AND [TotalProcurementQuantity] IS NOT NULL AND [TotalProcurementQuantity] > 0 AND [ProcurementUnitId] IS NOT NULL AND [UnitPricePerProcurementUnit] IS NOT NULL AND [UnitPricePerProcurementUnit] >= 0 AND [UnitPricePerPackage] IS NULL)");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchLines_IngredientSuppliers_IngredientSupplierId",
                        column: x => x.IngredientSupplierId,
                        principalTable: "IngredientSuppliers",
                        principalColumn: "IngredientSupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchLines_PurchaseOrderBatches_PurchaseOrderBatchId",
                        column: x => x.PurchaseOrderBatchId,
                        principalTable: "PurchaseOrderBatches",
                        principalColumn: "PurchaseOrderBatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchLines_Units_PackageUnitId",
                        column: x => x.PackageUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderBatchLines_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderBatchId = table.Column<int>(type: "int", nullable: true),
                    MasterPurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDeliveryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    SentByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.PurchaseOrderId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_PurchaseOrderBatches_PurchaseOrderBatchId",
                        column: x => x.PurchaseOrderBatchId,
                        principalTable: "PurchaseOrderBatches",
                        principalColumn: "PurchaseOrderBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_PurchaseOrders_MasterPurchaseOrderId",
                        column: x => x.MasterPurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Staffs_SentByStaffId",
                        column: x => x.SentByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleOptimizationAssignments",
                columns: table => new
                {
                    ScheduleOptimizationAssignmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleOptimizationProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOptimizationAssignments", x => x.ScheduleOptimizationAssignmentId);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationAssignments_ScheduleOptimizationProposals_ScheduleOptimizationProposalId",
                        column: x => x.ScheduleOptimizationProposalId,
                        principalTable: "ScheduleOptimizationProposals",
                        principalColumn: "ScheduleOptimizationProposalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleOptimizationAssignments_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalShiftScheduleSources",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    StaffShiftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShiftScheduleSources", x => new { x.OperationalShiftId, x.StaffShiftId });
                    table.ForeignKey(
                        name: "FK_OperationalShiftScheduleSources_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationalShiftScheduleSources_StaffShifts_StaffShiftId",
                        column: x => x.StaffShiftId,
                        principalTable: "StaffShifts",
                        principalColumn: "StaffShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkShiftOpenApprovalRequests",
                columns: table => new
                {
                    WorkShiftOpenApprovalRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    DecidedByStaffId = table.Column<int>(type: "int", nullable: true),
                    SourceStaffShiftId = table.Column<int>(type: "int", nullable: true),
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinutesLate = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShiftOpenApprovalRequests", x => x.WorkShiftOpenApprovalRequestId);
                    table.ForeignKey(
                        name: "FK_WorkShiftOpenApprovalRequests_PosTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShiftOpenApprovalRequests_StaffShifts_SourceStaffShiftId",
                        column: x => x.SourceStaffShiftId,
                        principalTable: "StaffShifts",
                        principalColumn: "StaffShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShiftOpenApprovalRequests_Staffs_DecidedByStaffId",
                        column: x => x.DecidedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShiftOpenApprovalRequests_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShiftOpenApprovalRequests_Stores_StoreId",
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
                    CurrentOperatorStaffId = table.Column<int>(type: "int", nullable: true),
                    OperatorChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    SourceStaffShiftId = table.Column<int>(type: "int", nullable: true),
                    OpenContext = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "LEGACY"),
                    OutsideScheduleReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoCloseAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpiryWarningLevel = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedEndingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ActualEndingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CashDiscrepancy = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "OPEN"),
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
                    LastLateOfflineSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PosTerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShifts", x => x.ShiftId);
                    table.CheckConstraint("CK_WorkShifts_ActualEndingCash", "[ActualEndingCash] IS NULL OR ([ActualEndingCash] >= 0 AND [ActualEndingCash] = FLOOR([ActualEndingCash]))");
                    table.CheckConstraint("CK_WorkShifts_OpenContext", "[OpenContext] IN ('WITHIN_SCHEDULE','LATE_FOR_SCHEDULE','OUTSIDE_SCHEDULE','LEGACY')");
                    table.CheckConstraint("CK_WorkShifts_StartingCash", "[StartingCash] >= 0 AND [StartingCash] = FLOOR([StartingCash])");
                    table.CheckConstraint("CK_WorkShifts_Status", "[Status] IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE','CLOSED','RECONCILIATION_REQUIRED')");
                    table.ForeignKey(
                        name: "FK_WorkShifts_PosTerminals_PosTerminalId",
                        column: x => x.PosTerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_StaffShifts_SourceStaffShiftId",
                        column: x => x.SourceStaffShiftId,
                        principalTable: "StaffShifts",
                        principalColumn: "StaffShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Staffs_ClosedByStaffId",
                        column: x => x.ClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkShifts_Staffs_CurrentOperatorStaffId",
                        column: x => x.CurrentOperatorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
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
                name: "RestockRequests",
                columns: table => new
                {
                    RestockRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StockAlertId = table.Column<int>(type: "int", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Legacy"),
                    SourceReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedForStoreId = table.Column<int>(type: "int", nullable: true),
                    NeedByDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    TargetStockProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ForecastEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourcingDecision = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    SourcingStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "UNALLOCATED"),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    RecipeId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SuggestedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SuggestionAnalysisWindowDays = table.Column<int>(type: "int", nullable: true),
                    SuggestionAvailableSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SuggestionMinLevelSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SuggestionAverageDailyUsageSnapshot = table.Column<decimal>(type: "decimal(18,5)", nullable: true),
                    SuggestionLeadTimeDaysSnapshot = table.Column<int>(type: "int", nullable: true),
                    SuggestionIncomingQuantitySnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SuggestionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandledByStaffId = table.Column<int>(type: "int", nullable: true),
                    HandledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByStaffId = table.Column<int>(type: "int", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedRemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    RemainingClosedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RemainingClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemainingCloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockRequests", x => x.RestockRequestId);
                    table.CheckConstraint("CK_RestockRequests_Identity", "\r\n(\r\n  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)\r\n)");
                    table.ForeignKey(
                        name: "FK_RestockRequests_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Staffs_AcceptedByStaffId",
                        column: x => x.AcceptedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Staffs_HandledByStaffId",
                        column: x => x.HandledByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Staffs_RemainingClosedByStaffId",
                        column: x => x.RemainingClosedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_StockAlerts_StockAlertId",
                        column: x => x.StockAlertId,
                        principalTable: "StockAlerts",
                        principalColumn: "StockAlertId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Stores_CreatedForStoreId",
                        column: x => x.CreatedForStoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequests_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAlertTransitions",
                columns: table => new
                {
                    StockAlertTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockAlertId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreviousAlertType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewAlertType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreviousSeverity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NewSeverity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OnHandSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReservedSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AvailableSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinLevelSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlertTransitions", x => x.StockAlertTransitionId);
                    table.ForeignKey(
                        name: "FK_StockAlertTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlertTransitions_StockAlerts_StockAlertId",
                        column: x => x.StockAlertId,
                        principalTable: "StockAlerts",
                        principalColumn: "StockAlertId",
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
                name: "StoreMenuItemAudits",
                columns: table => new
                {
                    StoreMenuItemAuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreMenuItemId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    DrinkSizeId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldIsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NewIsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OldPriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NewPriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OldEffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewEffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OldEffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewEffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CatalogVersionBefore = table.Column<long>(type: "bigint", nullable: false),
                    CatalogVersionAfter = table.Column<long>(type: "bigint", nullable: false),
                    ItemRowVersionBefore = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    ItemRowVersionAfter = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    OldDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreMenuItemAudits", x => x.StoreMenuItemAuditId);
                    table.ForeignKey(
                        name: "FK_StoreMenuItemAudits_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreMenuItemAudits_StoreMenuItems_StoreMenuItemId",
                        column: x => x.StoreMenuItemId,
                        principalTable: "StoreMenuItems",
                        principalColumn: "StoreMenuItemId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "InventoryNegativeApprovalLines",
                columns: table => new
                {
                    InventoryNegativeApprovalLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryNegativeApprovalId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    BeforeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IssueQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ProjectedAfterQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EffectiveMaxNegativeQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    InventoryRowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryNegativeApprovalLines", x => x.InventoryNegativeApprovalLineId);
                    table.CheckConstraint("CK_InventoryNegativeApprovalLine_Identity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryNegativeApprovalLine_Quantity", "[IssueQty] > 0 AND [EffectiveMaxNegativeQty] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryNegativeApprovalLines_InventoryNegativeApprovals_InventoryNegativeApprovalId",
                        column: x => x.InventoryNegativeApprovalId,
                        principalTable: "InventoryNegativeApprovals",
                        principalColumn: "InventoryNegativeApprovalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IceCarryOvers",
                columns: table => new
                {
                    IceCarryOverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FromOperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    ToOperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    FromIceAllocationId = table.Column<int>(type: "int", nullable: false),
                    ToIceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    HandedOverByStaffId = table.Column<int>(type: "int", nullable: false),
                    ReceivedByStaffId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceCarryOvers", x => x.IceCarryOverId);
                    table.CheckConstraint("CK_IceCarryOvers_DifferentShifts", "[FromOperationalShiftId] <> [ToOperationalShiftId]");
                    table.CheckConstraint("CK_IceCarryOvers_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceCarryOvers_Status", "[Status] IN ('Pending','Confirmed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_IceAllocations_FromIceAllocationId",
                        column: x => x.FromIceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_IceAllocations_ToIceAllocationId",
                        column: x => x.ToIceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_OperationalShifts_FromOperationalShiftId",
                        column: x => x.FromOperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_OperationalShifts_ToOperationalShiftId",
                        column: x => x.ToOperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_Staffs_HandedOverByStaffId",
                        column: x => x.HandedOverByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceCarryOvers_Staffs_ReceivedByStaffId",
                        column: x => x.ReceivedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceSupplementalIssues",
                columns: table => new
                {
                    IceSupplementalIssueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReservationApplied = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceSupplementalIssues", x => x.IceSupplementalIssueId);
                    table.CheckConstraint("CK_IceSupplementalIssues_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceSupplementalIssues_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_IceAllocations_IceAllocationId",
                        column: x => x.IceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_RejectedByStaffId",
                        column: x => x.RejectedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceSupplementalIssues_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchReceipts",
                columns: table => new
                {
                    BranchReceiptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    SourceInventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReceiptKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchReceipts", x => x.BranchReceiptId);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_InventoryTransfers_SourceInventoryTransferId",
                        column: x => x.SourceInventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_Staffs_ReceivedByStaffId",
                        column: x => x.ReceivedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalShiftWorkShifts",
                columns: table => new
                {
                    OperationalShiftId = table.Column<int>(type: "int", nullable: false),
                    WorkShiftId = table.Column<int>(type: "int", nullable: false),
                    LinkedByStaffId = table.Column<int>(type: "int", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalShiftWorkShifts", x => new { x.OperationalShiftId, x.WorkShiftId });
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_OperationalShifts_OperationalShiftId",
                        column: x => x.OperationalShiftId,
                        principalTable: "OperationalShifts",
                        principalColumn: "OperationalShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_Staffs_LinkedByStaffId",
                        column: x => x.LinkedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalShiftWorkShifts_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
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
                    PaymentStatusId = table.Column<int>(type: "int", nullable: false),
                    OrderTypeId = table.Column<int>(type: "int", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    WorkShiftId = table.Column<int>(type: "int", nullable: true),
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiverPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VoucherDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PointsUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalCogs = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GrossProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                        name: "FK_Orders_PosTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
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
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TerminalName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientIpHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    DeviceFingerprintHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ApproverStaffId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProtectedOtpPayload = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PayloadFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: ""),
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
                    NewValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
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
                        name: "FK_OtpChallenges_Staffs_ConfirmedByStaffId",
                        column: x => x.ConfirmedByStaffId,
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
                name: "PosAccessSessions",
                columns: table => new
                {
                    PosAccessSessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkShiftId = table.Column<int>(type: "int", nullable: true),
                    ExchangeContextId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedByStaffId = table.Column<int>(type: "int", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosAccessSessions", x => x.PosAccessSessionId);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_PosTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_Staffs_EndedByStaffId",
                        column: x => x.EndedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAccessSessions_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestockFulfillmentPostings",
                columns: table => new
                {
                    RestockFulfillmentPostingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentLineId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockFulfillmentPostings", x => x.RestockFulfillmentPostingId);
                    table.CheckConstraint("CK_RestockFulfillmentPosting_Identity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_RestockFulfillmentPosting_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_RestockFulfillmentPostings_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockFulfillmentPostings_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockFulfillmentPostings_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockFulfillmentPostings_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestockRequestFulfillments",
                columns: table => new
                {
                    RestockRequestFulfillmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlannedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockRequestFulfillments", x => x.RestockRequestFulfillmentId);
                    table.ForeignKey(
                        name: "FK_RestockRequestFulfillments_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequestFulfillments_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
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
                    StoreMenuItemId = table.Column<int>(type: "int", nullable: true),
                    DrinkSizeId = table.Column<int>(type: "int", nullable: true),
                    RecipeIdSnapshot = table.Column<int>(type: "int", nullable: true),
                    DrinkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AcceptedBasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PriceSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AcceptedCatalogVersion = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IceLevelPercent = table.Column<int>(type: "int", nullable: true),
                    IceIngredientId = table.Column<int>(type: "int", nullable: true),
                    BaseIceQuantityBaseUnit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AppliedIceQuantityBaseUnit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CostStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UnitCogs = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalCogs = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetails", x => x.OrderDetailId);
                    table.CheckConstraint("CK_OrderDetails_IceLevelPercent", "[IceLevelPercent] IS NULL OR [IceLevelPercent] IN (0, 50, 100)");
                    table.CheckConstraint("CK_OrderDetails_IceSnapshot", "([IceLevelPercent] IS NULL AND [IceIngredientId] IS NULL AND [BaseIceQuantityBaseUnit] IS NULL AND [AppliedIceQuantityBaseUnit] IS NULL) OR ([IceLevelPercent] IS NOT NULL AND [IceIngredientId] IS NOT NULL AND [BaseIceQuantityBaseUnit] IS NOT NULL AND [AppliedIceQuantityBaseUnit] IS NOT NULL AND [BaseIceQuantityBaseUnit] >= 0 AND [AppliedIceQuantityBaseUnit] >= 0 AND [AppliedIceQuantityBaseUnit] <= [BaseIceQuantityBaseUnit])");
                    table.ForeignKey(
                        name: "FK_OrderDetails_DrinkSizes_DrinkSizeId",
                        column: x => x.DrinkSizeId,
                        principalTable: "DrinkSizes",
                        principalColumn: "DrinkSizeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Drinks_DrinkId",
                        column: x => x.DrinkId,
                        principalTable: "Drinks",
                        principalColumn: "DrinkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Ingredients_IceIngredientId",
                        column: x => x.IceIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Recipes_RecipeIdSnapshot",
                        column: x => x.RecipeIdSnapshot,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "SizeId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderDetails_StoreMenuItems_StoreMenuItemId",
                        column: x => x.StoreMenuItemId,
                        principalTable: "StoreMenuItems",
                        principalColumn: "StoreMenuItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderRefunds",
                columns: table => new
                {
                    OrderRefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RefundKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostStatus = table.Column<int>(type: "int", nullable: false),
                    ReversedCogs = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InventoryReversalStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ProcessingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByStaffId = table.Column<int>(type: "int", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderRefunds", x => x.OrderRefundId);
                    table.CheckConstraint("CK_OrderRefunds_RefundAmount", "[RefundAmount] >= 0");
                    table.CheckConstraint("CK_OrderRefunds_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_OrderRefunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderRefunds_Staffs_CompletedByStaffId",
                        column: x => x.CompletedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderRefunds_Staffs_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderRefunds_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
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
                    StoreId = table.Column<int>(type: "int", nullable: true),
                    WorkShiftId = table.Column<int>(type: "int", nullable: true),
                    PaidByStaffId = table.Column<int>(type: "int", nullable: true),
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.ForeignKey(
                        name: "FK_Payments_PosTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "TerminalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Staffs_PaidByStaffId",
                        column: x => x.PaidByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "ShiftId",
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
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "PosRecommendationExposures",
                columns: table => new
                {
                    PosRecommendationExposureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecommendationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Variant = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosRecommendationExposures", x => x.PosRecommendationExposureId);
                    table.ForeignKey(
                        name: "FK_PosRecommendationExposures_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PosRecommendationExposures_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffNotifications",
                columns: table => new
                {
                    StaffNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RecipientStaffId = table.Column<int>(type: "int", nullable: false),
                    OtpChallengeId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "INFO"),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    MeaningfulVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmailAttempted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmailSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmailErrorSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffNotifications", x => x.StaffNotificationId);
                    table.ForeignKey(
                        name: "FK_StaffNotifications_OtpChallenges_OtpChallengeId",
                        column: x => x.OtpChallengeId,
                        principalTable: "OtpChallenges",
                        principalColumn: "OtpChallengeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffNotifications_Staffs_RecipientStaffId",
                        column: x => x.RecipientStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffNotifications_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDetails",
                columns: table => new
                {
                    InventoryTransferDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    RestockRequestId = table.Column<int>(type: "int", nullable: true),
                    RestockRequestFulfillmentId = table.Column<int>(type: "int", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    ParentInventoryTransferDetailId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DispatchedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
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
                    table.CheckConstraint("CK_InventoryTransferDetail_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryTransferDetail_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryTransferDetail_UnitPrice", "[UnitPrice] IS NULL OR [UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_InventoryTransferDetails_ParentInventoryTransferDetailId",
                        column: x => x.ParentInventoryTransferDetailId,
                        principalTable: "InventoryTransferDetails",
                        principalColumn: "InventoryTransferDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_RestockRequestFulfillments_RestockRequestFulfillmentId",
                        column: x => x.RestockRequestFulfillmentId,
                        principalTable: "RestockRequestFulfillments",
                        principalColumn: "RestockRequestFulfillmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDetails_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
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
                    RecipeIdSnapshot = table.Column<int>(type: "int", nullable: true),
                    ToppingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityPerDrinkSnapshot = table.Column<decimal>(type: "decimal(18,5)", nullable: false, defaultValue: 1m),
                    QuantityUnitSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "RECIPE_PORTION"),
                    PriceTreatmentSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "ADD_TOPPING_PRICE"),
                    CostTreatmentSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "ADD_TOPPING_RECIPE_COST"),
                    CostStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalCogs = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderToppings", x => x.OrderToppingId);
                    table.CheckConstraint("CK_OrderToppings_CostTreatmentSnapshot", "[CostTreatmentSnapshot] IN ('INCLUDED_IN_DRINK_RECIPE','ADD_TOPPING_RECIPE_COST')");
                    table.CheckConstraint("CK_OrderToppings_PriceTreatmentSnapshot", "[PriceTreatmentSnapshot] IN ('INCLUDED_IN_BASE_PRICE','ADD_TOPPING_PRICE')");
                    table.CheckConstraint("CK_OrderToppings_QuantityPerDrinkSnapshot", "[QuantityPerDrinkSnapshot] > 0");
                    table.CheckConstraint("CK_OrderToppings_QuantityUnitSnapshot", "[QuantityUnitSnapshot] = 'RECIPE_PORTION'");
                    table.ForeignKey(
                        name: "FK_OrderToppings_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderToppings_Recipes_RecipeIdSnapshot",
                        column: x => x.RecipeIdSnapshot,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderToppings_Toppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "Toppings",
                        principalColumn: "ToppingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosRecommendationExposureItems",
                columns: table => new
                {
                    PosRecommendationExposureItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PosRecommendationExposureId = table.Column<long>(type: "bigint", nullable: false),
                    TriggerDrinkId = table.Column<int>(type: "int", nullable: false),
                    RecommendedDrinkId = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    WasDisplayed = table.Column<bool>(type: "bit", nullable: false),
                    WasClicked = table.Column<bool>(type: "bit", nullable: false),
                    WasAdded = table.Column<bool>(type: "bit", nullable: false),
                    WasPurchased = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosRecommendationExposureItems", x => x.PosRecommendationExposureItemId);
                    table.ForeignKey(
                        name: "FK_PosRecommendationExposureItems_PosRecommendationExposures_PosRecommendationExposureId",
                        column: x => x.PosRecommendationExposureId,
                        principalTable: "PosRecommendationExposures",
                        principalColumn: "PosRecommendationExposureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesCostGaps",
                columns: table => new
                {
                    SalesCostGapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OrderDetailId = table.Column<int>(type: "int", nullable: false),
                    OrderToppingId = table.Column<int>(type: "int", nullable: true),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AllocatedCostQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MissingCostQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCostGaps", x => x.SalesCostGapId);
                    table.CheckConstraint("CK_SalesCostGaps_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesCostGaps_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostGaps_OrderToppings_OrderToppingId",
                        column: x => x.OrderToppingId,
                        principalTable: "OrderToppings",
                        principalColumn: "OrderToppingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostGaps_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundCostGaps",
                columns: table => new
                {
                    RefundCostGapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderRefundId = table.Column<int>(type: "int", nullable: false),
                    SalesCostGapId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundCostGaps", x => x.RefundCostGapId);
                    table.CheckConstraint("CK_RefundCostGaps_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RefundCostGaps_OrderRefunds_OrderRefundId",
                        column: x => x.OrderRefundId,
                        principalTable: "OrderRefunds",
                        principalColumn: "OrderRefundId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCostGaps_SalesCostGaps_SalesCostGapId",
                        column: x => x.SalesCostGapId,
                        principalTable: "SalesCostGaps",
                        principalColumn: "SalesCostGapId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchReceiptLines",
                columns: table => new
                {
                    BranchReceiptLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchReceiptId = table.Column<int>(type: "int", nullable: false),
                    RestockRequestId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: true),
                    SourceInventoryTransferDetailId = table.Column<int>(type: "int", nullable: true),
                    SourceTransferCostAllocationId = table.Column<long>(type: "bigint", nullable: true),
                    RestockRequestFulfillmentId = table.Column<int>(type: "int", nullable: true),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    RecipeId = table.Column<int>(type: "int", nullable: true),
                    InputQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    InputUnitId = table.Column<int>(type: "int", nullable: false),
                    ReceivedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RejectedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ReceivedPackQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AcceptedPackQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ReceivedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RejectedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AcceptedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InventoryPostingBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    InventoryBaseUnitId = table.Column<int>(type: "int", nullable: true),
                    ProcurementToInventoryFactor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionIssueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: true),
                    ActualPackagePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PackageQuantitySnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PackageUnitIdSnapshot = table.Column<int>(type: "int", nullable: true),
                    BaseUnitCostSnapshot = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineTotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchReceiptLines", x => x.BranchReceiptLineId);
                    table.CheckConstraint("CK_BranchReceiptLines_Identity", "\r\n(\r\n  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)\r\n  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)\r\n)");
                    table.CheckConstraint("CK_BranchReceiptLines_Quantities", "[InputQuantity] > 0 AND [ReceivedBaseQuantity] >= 0 AND [RejectedBaseQuantity] >= 0 AND (([ReceivedProcurementQuantity] IS NOT NULL AND [ReceivedProcurementQuantity] > 0 AND [AcceptedProcurementQuantity] IS NOT NULL AND [AcceptedProcurementQuantity] >= 0 AND [RejectedProcurementQuantity] IS NOT NULL AND [RejectedProcurementQuantity] >= 0) OR ([ReceivedBaseQuantity] + [RejectedBaseQuantity]) > 0)");
                    table.CheckConstraint("CK_BranchReceiptLines_RejectionReason", "([RejectedBaseQuantity] = 0 AND ([RejectedProcurementQuantity] IS NULL OR [RejectedProcurementQuantity] = 0)) OR (LEN(LTRIM(RTRIM([RejectionReason]))) > 0 AND LEN(LTRIM(RTRIM([RejectionIssueType]))) > 0)");
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_BranchReceipts_BranchReceiptId",
                        column: x => x.BranchReceiptId,
                        principalTable: "BranchReceipts",
                        principalColumn: "BranchReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_IngredientSuppliers_IngredientSupplierId",
                        column: x => x.IngredientSupplierId,
                        principalTable: "IngredientSuppliers",
                        principalColumn: "IngredientSupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_InventoryTransferDetails_SourceInventoryTransferDetailId",
                        column: x => x.SourceInventoryTransferDetailId,
                        principalTable: "InventoryTransferDetails",
                        principalColumn: "InventoryTransferDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_RestockRequestFulfillments_RestockRequestFulfillmentId",
                        column: x => x.RestockRequestFulfillmentId,
                        principalTable: "RestockRequestFulfillments",
                        principalColumn: "RestockRequestFulfillmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Units_InputUnitId",
                        column: x => x.InputUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Units_InventoryBaseUnitId",
                        column: x => x.InventoryBaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Units_PackageUnitIdSnapshot",
                        column: x => x.PackageUnitIdSnapshot,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReceiptLines_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
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
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferDetailId = table.Column<int>(type: "int", nullable: true),
                    ReferenceOrderId = table.Column<int>(type: "int", nullable: true),
                    ProductionRunId = table.Column<int>(type: "int", nullable: true),
                    SourceRecipeId = table.Column<int>(type: "int", nullable: true),
                    InventoryConsolidationRunId = table.Column<int>(type: "int", nullable: true),
                    BranchReceiptLineId = table.Column<int>(type: "int", nullable: true),
                    OrderRefundId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.CheckConstraint("CK_InventoryTransaction_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryTransaction_TotalCost", "[TotalCost] IS NULL OR [TotalCost] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_UnitCost", "[UnitCost] IS NULL OR [UnitCost] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_BranchReceiptLines_BranchReceiptLineId",
                        column: x => x.BranchReceiptLineId,
                        principalTable: "BranchReceiptLines",
                        principalColumn: "BranchReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryConsolidationRuns_InventoryConsolidationRunId",
                        column: x => x.InventoryConsolidationRunId,
                        principalTable: "InventoryConsolidationRuns",
                        principalColumn: "InventoryConsolidationRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryDocumentDetails_InventoryDocumentDetailId",
                        column: x => x.InventoryDocumentDetailId,
                        principalTable: "InventoryDocumentDetails",
                        principalColumn: "InventoryDocumentDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryDocuments_InventoryDocumentId",
                        column: x => x.InventoryDocumentId,
                        principalTable: "InventoryDocuments",
                        principalColumn: "InventoryDocumentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransferDetails_InventoryTransferDetailId",
                        column: x => x.InventoryTransferDetailId,
                        principalTable: "InventoryTransferDetails",
                        principalColumn: "InventoryTransferDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_OrderRefunds_OrderRefundId",
                        column: x => x.OrderRefundId,
                        principalTable: "OrderRefunds",
                        principalColumn: "OrderRefundId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Orders_ReferenceOrderId",
                        column: x => x.ReferenceOrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Recipes_SourceRecipeId",
                        column: x => x.SourceRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IceInventoryPostings",
                columns: table => new
                {
                    IceInventoryPostingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IceAllocationId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    PostingType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceInventoryPostings", x => x.IceInventoryPostingId);
                    table.CheckConstraint("CK_IceInventoryPostings_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_IceInventoryPostings_Type", "[PostingType] IN ('VarianceOut')");
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_IceAllocations_IceAllocationId",
                        column: x => x.IceAllocationId,
                        principalTable: "IceAllocations",
                        principalColumn: "IceAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IceInventoryPostings_Staffs_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryNegativeCostGaps",
                columns: table => new
                {
                    InventoryNegativeCostGapId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StoreInventoryId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    SalesCostGapId = table.Column<int>(type: "int", nullable: true),
                    InventoryDocumentDetailId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: true),
                    InventoryNegativeApprovalId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    OutstandingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryNegativeCostGaps", x => x.InventoryNegativeCostGapId);
                    table.CheckConstraint("CK_InventoryNegativeCostGap_Identity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryNegativeCostGap_Quantity", "[OriginalQuantity] > 0 AND [OutstandingQuantity] >= 0 AND [OutstandingQuantity] <= [OriginalQuantity]");
                    table.CheckConstraint("CK_InventoryNegativeCostGap_Source", "[SourceType] IN ('POS_SALE','MANUAL_DOCUMENT','LEGACY_BALANCE')");
                    table.CheckConstraint("CK_InventoryNegativeCostGap_Status", "[Status] IN ('OPEN','PARTIALLY_SETTLED','SETTLED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_InventoryNegativeCostGaps_InventoryDocumentDetails_InventoryDocumentDetailId",
                        column: x => x.InventoryDocumentDetailId,
                        principalTable: "InventoryDocumentDetails",
                        principalColumn: "InventoryDocumentDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeCostGaps_InventoryNegativeApprovals_InventoryNegativeApprovalId",
                        column: x => x.InventoryNegativeApprovalId,
                        principalTable: "InventoryNegativeApprovals",
                        principalColumn: "InventoryNegativeApprovalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeCostGaps_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeCostGaps_SalesCostGaps_SalesCostGapId",
                        column: x => x.SalesCostGapId,
                        principalTable: "SalesCostGaps",
                        principalColumn: "SalesCostGapId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryNegativeCostGaps_StoreInventories_StoreInventoryId",
                        column: x => x.StoreInventoryId,
                        principalTable: "StoreInventories",
                        principalColumn: "StoreInventoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestockRequestTransitions",
                columns: table => new
                {
                    RestockRequestTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BranchReceiptId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: true),
                    QuantityBefore = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SuggestionSnapshotVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SuggestionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockRequestTransitions", x => x.RestockRequestTransitionId);
                    table.ForeignKey(
                        name: "FK_RestockRequestTransitions_BranchReceipts_BranchReceiptId",
                        column: x => x.BranchReceiptId,
                        principalTable: "BranchReceipts",
                        principalColumn: "BranchReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequestTransitions_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequestTransitions_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequestTransitions_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRequestTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
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
                        name: "FK_InventoryCostAllocations_InventoryDocumentDetails_InventoryDocumentDetailId",
                        column: x => x.InventoryDocumentDetailId,
                        principalTable: "InventoryDocumentDetails",
                        principalColumn: "InventoryDocumentDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostGapSettlements",
                columns: table => new
                {
                    InventoryCostGapSettlementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryNegativeCostGapId = table.Column<long>(type: "bigint", nullable: false),
                    InboundInventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostGapSettlements", x => x.InventoryCostGapSettlementId);
                    table.CheckConstraint("CK_InventoryCostGapSettlement_Quantity", "[Quantity] > 0 AND [UnitCost] >= 0 AND [TotalCost] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryCostGapSettlements_InventoryNegativeCostGaps_InventoryNegativeCostGapId",
                        column: x => x.InventoryNegativeCostGapId,
                        principalTable: "InventoryNegativeCostGaps",
                        principalColumn: "InventoryNegativeCostGapId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                columns: table => new
                {
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    SourceProductionRunId = table.Column<int>(type: "int", nullable: true),
                    SourceOrderRefundId = table.Column<int>(type: "int", nullable: true),
                    SourceInventoryDocumentDetailId = table.Column<int>(type: "int", nullable: true),
                    SourceBranchReceiptLineId = table.Column<int>(type: "int", nullable: true),
                    SourceTransferCostAllocationId = table.Column<long>(type: "bigint", nullable: true),
                    SourceTransferDiscrepancyPostingId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.InventoryCostLayerId);
                    table.CheckConstraint("CK_InventoryCostLayers_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_BranchReceiptLines_SourceBranchReceiptLineId",
                        column: x => x.SourceBranchReceiptLineId,
                        principalTable: "BranchReceiptLines",
                        principalColumn: "BranchReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryDocumentDetails_SourceInventoryDocumentDetailId",
                        column: x => x.SourceInventoryDocumentDetailId,
                        principalTable: "InventoryDocumentDetails",
                        principalColumn: "InventoryDocumentDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_OrderRefunds_SourceOrderRefundId",
                        column: x => x.SourceOrderRefundId,
                        principalTable: "OrderRefunds",
                        principalColumn: "OrderRefundId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_PreparedItems_PreparedItemId",
                        column: x => x.PreparedItemId,
                        principalTable: "PreparedItems",
                        principalColumn: "PreparedItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_ProductionRuns_SourceProductionRunId",
                        column: x => x.SourceProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferCostAllocations",
                columns: table => new
                {
                    InventoryTransferCostAllocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryTransferDetailId = table.Column<int>(type: "int", nullable: false),
                    SourceInventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransferCostAllocations", x => x.InventoryTransferCostAllocationId);
                    table.CheckConstraint("CK_InventoryTransferCostAllocation_Quantity", "[Quantity] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [UnitCost] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferCostAllocations_InventoryCostLayers_SourceInventoryCostLayerId",
                        column: x => x.SourceInventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferCostAllocations_InventoryTransferDetails_InventoryTransferDetailId",
                        column: x => x.InventoryTransferDetailId,
                        principalTable: "InventoryTransferDetails",
                        principalColumn: "InventoryTransferDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionCostAllocations",
                columns: table => new
                {
                    ProductionCostAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRunId = table.Column<int>(type: "int", nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false),
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionCostAllocations", x => x.ProductionCostAllocationId);
                    table.ForeignKey(
                        name: "FK_ProductionCostAllocations_InventoryCostLayers_InventoryCostLayerId",
                        column: x => x.InventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionCostAllocations_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionCostAllocations_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesCostAllocations",
                columns: table => new
                {
                    SalesCostAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OrderDetailId = table.Column<int>(type: "int", nullable: false),
                    OrderToppingId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false),
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCostAllocations", x => x.SalesCostAllocationId);
                    table.CheckConstraint("CK_SalesCostAllocations_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesCostAllocations_InventoryCostLayers_InventoryCostLayerId",
                        column: x => x.InventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostAllocations_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostAllocations_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostAllocations_OrderToppings_OrderToppingId",
                        column: x => x.OrderToppingId,
                        principalTable: "OrderToppings",
                        principalColumn: "OrderToppingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCostAllocations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDiscrepancyPostings",
                columns: table => new
                {
                    InventoryTransferDiscrepancyPostingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryTransferDetailId = table.Column<int>(type: "int", nullable: false),
                    InventoryTransferCostAllocationId = table.Column<long>(type: "bigint", nullable: false),
                    RelatedPostingId = table.Column<long>(type: "bigint", nullable: true),
                    PostingType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransferDiscrepancyPostings", x => x.InventoryTransferDiscrepancyPostingId);
                    table.CheckConstraint("CK_InventoryTransferDiscrepancyPosting_QuantityCost", "[Quantity] > 0 AND [UnitCost] > 0 AND [TotalCost] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyPostings_InventoryTransferCostAllocations_InventoryTransferCostAllocationId",
                        column: x => x.InventoryTransferCostAllocationId,
                        principalTable: "InventoryTransferCostAllocations",
                        principalColumn: "InventoryTransferCostAllocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyPostings_InventoryTransferDetails_InventoryTransferDetailId",
                        column: x => x.InventoryTransferDetailId,
                        principalTable: "InventoryTransferDetails",
                        principalColumn: "InventoryTransferDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyPostings_InventoryTransferDiscrepancyPostings_RelatedPostingId",
                        column: x => x.RelatedPostingId,
                        principalTable: "InventoryTransferDiscrepancyPostings",
                        principalColumn: "InventoryTransferDiscrepancyPostingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyPostings_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundCostReversals",
                columns: table => new
                {
                    RefundCostReversalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderRefundId = table.Column<int>(type: "int", nullable: false),
                    SalesCostAllocationId = table.Column<int>(type: "int", nullable: false),
                    OriginalInventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    ReturnInventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    PreparedItemId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundCostReversals", x => x.RefundCostReversalId);
                    table.CheckConstraint("CK_RefundCostReversals_ExactlyOneIdentity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RefundCostReversals_InventoryCostLayers_OriginalInventoryCostLayerId",
                        column: x => x.OriginalInventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCostReversals_InventoryCostLayers_ReturnInventoryCostLayerId",
                        column: x => x.ReturnInventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "InventoryCostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCostReversals_InventoryTransactions_InventoryTransactionId",
                        column: x => x.InventoryTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCostReversals_OrderRefunds_OrderRefundId",
                        column: x => x.OrderRefundId,
                        principalTable: "OrderRefunds",
                        principalColumn: "OrderRefundId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCostReversals_SalesCostAllocations_SalesCostAllocationId",
                        column: x => x.SalesCostAllocationId,
                        principalTable: "SalesCostAllocations",
                        principalColumn: "SalesCostAllocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseAdviceFulfillmentPostings",
                columns: table => new
                {
                    PurchaseAdviceFulfillmentPostingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderLineAllocationId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    BranchReceiptLineId = table.Column<int>(type: "int", nullable: true),
                    CloseOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentLineId = table.Column<int>(type: "int", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseAdviceFulfillmentPostings", x => x.PurchaseAdviceFulfillmentPostingId);
                    table.CheckConstraint("CK_PurchaseAdviceFulfillmentPostings_QuantityPositive", "[Quantity] > 0");
                    table.CheckConstraint("CK_PurchaseAdviceFulfillmentPostings_SourceByType", "([PostingType] = 'ACCEPTED' AND [BranchReceiptLineId] IS NOT NULL AND [CloseOperationKey] IS NULL) OR ([PostingType] = 'CLOSED' AND [BranchReceiptLineId] IS NULL AND [CloseOperationKey] IS NOT NULL)");
                    table.CheckConstraint("CK_PurchaseAdviceFulfillmentPostings_Type", "[PostingType] IN ('ACCEPTED','CLOSED')");
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceFulfillmentPostings_BranchReceiptLines_BranchReceiptLineId",
                        column: x => x.BranchReceiptLineId,
                        principalTable: "BranchReceiptLines",
                        principalColumn: "BranchReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceFulfillmentPostings_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceFulfillmentPostings_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseAdviceLines",
                columns: table => new
                {
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseAdviceId = table.Column<int>(type: "int", nullable: false),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    RequestedPurchaseBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AllocatedToPoBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    AcceptedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    ClosedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    RequestedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    AllocatedToPoProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    AcceptedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    ClosedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    RestockSourcingAllocationId = table.Column<int>(type: "int", nullable: true),
                    NeededByDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActiveReservation = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseAdviceLines", x => x.PurchaseAdviceLineId);
                    table.CheckConstraint("CK_PurchaseAdviceLines_AcceptedNonNegative", "[AcceptedBaseQuantity] >= 0");
                    table.CheckConstraint("CK_PurchaseAdviceLines_AllocatedNonNegative", "[AllocatedToPoBaseQuantity] >= 0");
                    table.CheckConstraint("CK_PurchaseAdviceLines_ClosedNonNegative", "[ClosedBaseQuantity] >= 0");
                    table.CheckConstraint("CK_PurchaseAdviceLines_ProcurementRequestedPositive", "[RequestedProcurementQuantity] IS NULL OR [RequestedProcurementQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseAdviceLines_RequestedPositive", "[RequestedPurchaseBaseQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceLines_PurchaseAdvices_PurchaseAdviceId",
                        column: x => x.PurchaseAdviceId,
                        principalTable: "PurchaseAdvices",
                        principalColumn: "PurchaseAdviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceLines_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceLines_Units_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseAdviceLines_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    RestockRequestId = table.Column<int>(type: "int", nullable: true),
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: true),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    IngredientSupplierId = table.Column<int>(type: "int", nullable: false),
                    PackageUnitIdSnapshot = table.Column<int>(type: "int", nullable: true),
                    PackageQuantitySnapshot = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PackagePriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PackageCount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    OrderedPackageCount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    OrderedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OrderedPackQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PackSizeProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    OrderedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitPricePerPackage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPricePerProcurementUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RoundingSurplusProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AcceptedPackQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AcceptedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ClosedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    InventoryPostingBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InventoryBaseUnitId = table.Column<int>(type: "int", nullable: true),
                    ProcurementToInventoryFactor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ClosedRemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    CloseRemainingReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedRemainingByStaffId = table.Column<int>(type: "int", nullable: true),
                    ClosedRemainingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PromisedLeadTimeDaysSnapshot = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.PurchaseOrderLineId);
                    table.CheckConstraint("CK_PurchaseOrderLines_ClosedRemainingQuantity_NonNegative", "[ClosedRemainingQuantity] >= 0");
                    table.CheckConstraint("CK_PurchaseOrderLines_PurchaseModeAuthority", "([PurchaseMode] = 'Packaged' AND [OrderedPackageCount] IS NOT NULL AND [OrderedPackageCount] > 0 AND [OrderedPackageCount] = FLOOR([OrderedPackageCount]) AND [UnitPricePerPackage] IS NOT NULL AND [UnitPricePerPackage] >= 0 AND [UnitPricePerProcurementUnit] IS NULL AND ([PackSizeProcurementQuantity] IS NULL OR [PackSizeProcurementQuantity] > 0)) OR ([PurchaseMode] = 'Loose' AND [OrderedPackageCount] IS NULL AND [OrderedProcurementQuantity] IS NOT NULL AND [OrderedProcurementQuantity] > 0 AND [ProcurementUnitId] IS NOT NULL AND [UnitPricePerProcurementUnit] IS NOT NULL AND [UnitPricePerProcurementUnit] >= 0 AND [UnitPricePerPackage] IS NULL)");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_IngredientSuppliers_IngredientSupplierId",
                        column: x => x.IngredientSupplierId,
                        principalTable: "IngredientSuppliers",
                        principalColumn: "IngredientSupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseAdviceLines_PurchaseAdviceLineId",
                        column: x => x.PurchaseAdviceLineId,
                        principalTable: "PurchaseAdviceLines",
                        principalColumn: "PurchaseAdviceLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Staffs_ClosedRemainingByStaffId",
                        column: x => x.ClosedRemainingByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Units_InventoryBaseUnitId",
                        column: x => x.InventoryBaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Units_PackageUnitIdSnapshot",
                        column: x => x.PackageUnitIdSnapshot,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLineAllocations",
                columns: table => new
                {
                    PurchaseOrderLineAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderBatchLineId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    AllocatedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AllocatedPackageQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    AllocatedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DemandCoveredProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RoundingSurplusProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLineAllocations", x => x.PurchaseOrderLineAllocationId);
                    table.CheckConstraint("CK_PurchaseOrderLineAllocations_BasePositive", "[AllocatedBaseQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseOrderLineAllocations_PurchaseModeAuthority", "([PurchaseMode] = 'Packaged' AND [AllocatedPackageQuantity] IS NOT NULL AND [AllocatedPackageQuantity] > 0) OR ([PurchaseMode] = 'Loose' AND [AllocatedPackageQuantity] IS NULL AND [AllocatedProcurementQuantity] IS NOT NULL AND [AllocatedProcurementQuantity] > 0)");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineAllocations_PurchaseAdviceLines_PurchaseAdviceLineId",
                        column: x => x.PurchaseAdviceLineId,
                        principalTable: "PurchaseAdviceLines",
                        principalColumn: "PurchaseAdviceLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineAllocations_PurchaseOrderBatchLines_PurchaseOrderBatchLineId",
                        column: x => x.PurchaseOrderBatchLineId,
                        principalTable: "PurchaseOrderBatchLines",
                        principalColumn: "PurchaseOrderBatchLineId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineAllocations_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineAllocations_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineAllocations_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLineClosures",
                columns: table => new
                {
                    PurchaseOrderLineClosureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    ClosedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLineClosures", x => x.PurchaseOrderLineClosureId);
                    table.CheckConstraint("CK_PurchaseOrderLineClosures_ClosedBaseQuantity_Positive", "[ClosedBaseQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseOrderLineClosures_ClosedProcurementQuantity_Positive", "[ClosedProcurementQuantity] IS NULL OR [ClosedProcurementQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineClosures_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderReceiptPostings",
                columns: table => new
                {
                    PurchaseOrderReceiptPostingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    BranchReceiptLineId = table.Column<int>(type: "int", nullable: false),
                    AcceptedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RejectedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AcceptedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RejectedProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InventoryPostingBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: true),
                    InventoryBaseUnitId = table.Column<int>(type: "int", nullable: true),
                    ProcurementToInventoryFactor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PurchaseMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Packaged"),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderReceiptPostings", x => x.PurchaseOrderReceiptPostingId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptPostings_BranchReceiptLines_BranchReceiptLineId",
                        column: x => x.BranchReceiptLineId,
                        principalTable: "BranchReceiptLines",
                        principalColumn: "BranchReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptPostings_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptPostings_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptPostings_Units_InventoryBaseUnitId",
                        column: x => x.InventoryBaseUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptPostings_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestockSourcingAllocations",
                columns: table => new
                {
                    RestockSourcingAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestockRequestId = table.Column<int>(type: "int", nullable: false),
                    DecisionType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ProcurementQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProcurementUnitId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentLineId = table.Column<int>(type: "int", nullable: true),
                    PurchaseAdviceLineId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: true),
                    InventoryTransferId = table.Column<int>(type: "int", nullable: true),
                    ProductionRunId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockSourcingAllocations", x => x.RestockSourcingAllocationId);
                    table.CheckConstraint("CK_RestockSourcingAllocations_ActivePurchaseLink", "[Status] NOT IN ('ACTIVE','PENDING_PURCHASE') OR [DecisionType] <> 'PURCHASE' OR [PurchaseAdviceLineId] IS NOT NULL OR [PurchaseOrderLineId] IS NOT NULL OR [Status] = 'PENDING_PURCHASE'");
                    table.CheckConstraint("CK_RestockSourcingAllocations_Decision", "[DecisionType] IN ('TRANSFER','PURCHASE','PRODUCTION','REJECT')");
                    table.CheckConstraint("CK_RestockSourcingAllocations_Quantity", "[ProcurementQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "InventoryTransferId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_PurchaseAdviceLines_PurchaseAdviceLineId",
                        column: x => x.PurchaseAdviceLineId,
                        principalTable: "PurchaseAdviceLines",
                        principalColumn: "PurchaseAdviceLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_RestockRequests_RestockRequestId",
                        column: x => x.RestockRequestId,
                        principalTable: "RestockRequests",
                        principalColumn: "RestockRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Staffs_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Staffs_ReleasedByStaffId",
                        column: x => x.ReleasedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockSourcingAllocations_Units_ProcurementUnitId",
                        column: x => x.ProcurementUnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReceiptIssues",
                columns: table => new
                {
                    SupplierReceiptIssueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "int", nullable: false),
                    BranchReceiptId = table.Column<int>(type: "int", nullable: false),
                    BranchReceiptLineId = table.Column<int>(type: "int", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AffectedBaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DismissReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReportedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ResolvedByStaffId = table.Column<int>(type: "int", nullable: true),
                    DismissedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReceiptIssues", x => x.SupplierReceiptIssueId);
                    table.CheckConstraint("CK_SupplierReceiptIssue_AffectedQuantity", "[AffectedBaseQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_BranchReceiptLines_BranchReceiptLineId",
                        column: x => x.BranchReceiptLineId,
                        principalTable: "BranchReceiptLines",
                        principalColumn: "BranchReceiptLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_BranchReceipts_BranchReceiptId",
                        column: x => x.BranchReceiptId,
                        principalTable: "BranchReceipts",
                        principalColumn: "BranchReceiptId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "PurchaseOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_Staffs_DismissedByStaffId",
                        column: x => x.DismissedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_Staffs_ReportedByStaffId",
                        column: x => x.ReportedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_Staffs_ResolvedByStaffId",
                        column: x => x.ResolvedByStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssues_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReceiptIssueTransitions",
                columns: table => new
                {
                    SupplierReceiptIssueTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierReceiptIssueId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReceiptIssueTransitions", x => x.SupplierReceiptIssueTransitionId);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssueTransitions_Staffs_ActorStaffId",
                        column: x => x.ActorStaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReceiptIssueTransitions_SupplierReceiptIssues_SupplierReceiptIssueId",
                        column: x => x.SupplierReceiptIssueId,
                        principalTable: "SupplierReceiptIssues",
                        principalColumn: "SupplierReceiptIssueId",
                        onDelete: ReferentialAction.Cascade);
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
                    { 15, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "shiftsupervisor@cafechain.vn", null, "$2a$11$efK2U8lomCM2d.8RIBAJpOsC3kqnEphxxGQvt2MFWwgTiDX3MIGAe", true }
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
                table: "ScopeTypes",
                columns: new[] { "ScopeTypeId", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "COUNTRY", "Country" },
                    { 2, "PROVINCE", "Province" },
                    { 4, "WARD", "Ward" },
                    { 5, "STORE", "Store" }
                });

            migrationBuilder.InsertData(
                table: "Sizes",
                columns: new[] { "SizeId", "Active", "Description", "Name", "SizeCode", "SizeType" },
                values: new object[,]
                {
                    { 1, true, "Kích thước nhỏ", "S", "S", 1 },
                    { 2, true, "Kích thước trung bình", "M", "M", 1 },
                    { 3, true, "Kích thước lớn", "L", "L", 1 },
                    { 4, true, "Kích thước rất lớn", "XL", "XL", 1 },
                    { 5, true, "Kích thước 150ml", "150ml", "150ML", 2 },
                    { 6, true, "Kích thước 200ml", "200ml", "200ML", 2 },
                    { 7, true, "Kích thước 250ml", "250ml", "250ML", 2 },
                    { 8, true, "Kích thước 300ml", "300ml", "300ML", 2 }
                });

            migrationBuilder.InsertData(
                table: "StaffShiftStatuses",
                columns: new[] { "StaffShiftStatusId", "Code", "IsSystem", "Name" },
                values: new object[,]
                {
                    { 1, "SCHEDULED", true, "Đã lên lịch" },
                    { 2, "CANCELLED", true, "Đã hủy" }
                });

            migrationBuilder.InsertData(
                table: "Stores",
                columns: new[] { "StoreId", "Active", "Address", "CreatedAt", "Latitude", "Longitude", "Name", "Phone", "ProvinceId", "WardId" },
                values: new object[,]
                {
                    { 1, true, "123 Đại lộ Bình Dương", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, "CafeChain Thủ Dầu Một", "0900000001", null, null },
                    { 2, true, "456 Nguyễn Trãi", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, "CafeChain Thuận An", "0900000002", null, null },
                    { 3, true, "789 Lê Hồng Phong", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, "CafeChain Dĩ An", "0900000003", null, null }
                });

            migrationBuilder.InsertData(
                table: "Suppliers",
                columns: new[] { "SupplierId", "Active", "Address", "Code", "CreatedAt", "Name", "Note", "TaxCode", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, true, "Thành phố Hồ Chí Minh", "SUP001", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp A", "Nhà cung cấp nguyên liệu chính", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, true, "TP HCM", "SUP002", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp B", "Nhà cung cấp sữa và kem", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, true, "Đồng Nai", "SUP003", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp C", "Nhà cung cấp cà phê", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 4, true, "Hà Nội", "SUP004", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp D", "Nhà cung cấp syrup và trà", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 5, true, "Đà Nẵng", "SUP005", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nhà cung cấp E", "Nhà cung cấp matcha", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "SettingId", "Description", "SettingKey", "SettingValue" },
                values: new object[,]
                {
                    { 1, "Toạ độ trung tâm mặc định (VD: TPHCM - 10.8231, 106.6297)", "Map_Default_Center", "10.8231, 106.6297" },
                    { 2001, "Cho phép phiếu xuất ngoài với mục đích SALE gửi yêu cầu xuất âm.", "inventory_manual_external_export_negative_enabled", "false" },
                    { 2002, "Bắt buộc maker-checker cho phiếu xuất ngoài làm âm kho.", "inventory_manual_external_export_approval_required", "true" },
                    { 2003, "Hạn mức âm mặc định cho phiếu xuất ngoài.", "inventory_manual_external_export_default_max_negative_quantity", "0" },
                    { 2004, "Phiên bản policy phiếu xuất ngoài làm âm kho.", "inventory_manual_external_export_policy_version", "manual-export-v1" }
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
                    { 2, true, 3, "ING00002", "Sữa đặc demo lon 380 ml" },
                    { 3, true, 1, "ING00003", "Trà đen demo hộp 100 túi × 2 g" },
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
                table: "Recipes",
                columns: new[] { "RecipeId", "Active", "DrinkId", "EffectiveDate", "Name", "OutputQuantity", "OutputUnitId", "ParentVersionId", "PreparedItemId", "RecipeCode", "SizeId", "Status", "ToppingId", "YieldPercentage", "YieldVarianceTolerancePercent" },
                values: new object[,]
                {
                    { 5, true, null, null, "Trân châu đen", null, null, null, null, "RCP_TC_DEN", null, "Active", 1, 100m, null },
                    { 6, true, null, null, "Trân châu trắng", null, null, null, null, "RCP_TC_TRANG", null, "Active", 2, 100m, null }
                });

            migrationBuilder.InsertData(
                table: "Shifts",
                columns: new[] { "ShiftId", "Duration", "EndTime", "Name", "Notes", "StartTime", "StoreId" },
                values: new object[,]
                {
                    { 1, null, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 1 },
                    { 2, null, new TimeSpan(0, 18, 0, 0, 0), "Ca chiều", null, new TimeSpan(0, 12, 0, 0, 0), 1 },
                    { 3, null, new TimeSpan(0, 23, 0, 0, 0), "Ca tối", null, new TimeSpan(0, 18, 0, 0, 0), 1 },
                    { 4, null, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 2 },
                    { 5, null, new TimeSpan(0, 18, 0, 0, 0), "Ca chiều", null, new TimeSpan(0, 12, 0, 0, 0), 2 },
                    { 6, null, new TimeSpan(0, 12, 0, 0, 0), "Ca sáng", null, new TimeSpan(0, 6, 0, 0, 0), 3 },
                    { 7, null, new TimeSpan(0, 23, 0, 0, 0), "Ca tối", null, new TimeSpan(0, 18, 0, 0, 0), 3 }
                });

            migrationBuilder.InsertData(
                table: "Staffs",
                columns: new[] { "StaffId", "AccountId", "Active", "AvatarPublicId", "AvatarUrl", "CCCD", "CreatedAt", "DateOfBirth", "EmployeeStatus", "FullName", "Gender", "PosPinHash", "PosPinLockedUntilUtc", "StartDate", "StoreId" },
                values: new object[,]
                {
                    { 1, 1, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Chủ doanh nghiệp", 0, null, null, null, 1 },
                    { 2, 2, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Quản lý vùng TP.HCM", 0, null, null, null, 1 },
                    { 3, 3, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Quản lý chi nhánh Quận 1", 0, null, null, null, 1 },
                    { 4, 4, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Nhân viên bán hàng", 0, null, null, null, 1 },
                    { 5, 5, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Nhân viên kế toán kho", 0, null, null, null, 1 },
                    { 6, 6, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Quản trị hệ thống", 0, null, null, null, 1 },
                    { 15, 15, true, "avtdf_rfdc7o", "https://res.cloudinary.com/dzfizobk8/image/upload/v1784653191/avtdf_rfdc7o.jpg", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Ca trưởng chi nhánh", 0, null, null, null, 1 }
                });

            migrationBuilder.InsertData(
                table: "StoreInventoryWriterConfigurations",
                columns: new[] { "StoreId", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
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
                table: "SupplierContacts",
                columns: new[] { "SupplierContactId", "Active", "Email", "IsPrimary", "Name", "Note", "PhoneNumber", "Position", "SupplierId" },
                values: new object[,]
                {
                    { 1, true, "a@supplier.com", true, "Nguyễn Văn A", "Liên hệ chính", "0901111111", "Manager", 1 },
                    { 2, true, "b@supplier.com", true, "Trần Văn B", "Phụ trách bán hàng", "0902222222", "Sales", 2 },
                    { 3, true, "c@supplier.com", true, "Lê Văn C", "Chủ doanh nghiệp", "0903333333", "Owner", 3 },
                    { 4, true, "d@supplier.com", true, "Phạm Văn D", "Giám đốc", "0904444444", "Director", 4 },
                    { 5, true, "e@supplier.com", true, "Hoàng Văn E", "Quản lý kinh doanh", "0905555555", "Manager", 5 }
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
                columns: new[] { "IngredientSupplierId", "Active", "CurrentPrice", "CurrentProcurementUnitPrice", "IngredientId", "IsPrimary", "LeadTimeDays", "LooseMinimumOrderQuantity", "LoosePriceMode", "LooseProcurementUnitId", "LooseQuantityStep", "MinimumOrderPackageCount", "Note", "PackageQuantity", "SupplierId", "UnitId" },
                values: new object[,]
                {
                    { 1, true, 22000m, null, 6, true, 1, null, "INDEPENDENT", null, null, 1, "Đường Biên Hòa", 1m, 1, 2 },
                    { 2, true, 27000m, null, 2, true, 2, null, "INDEPENDENT", null, null, 24, "Sữa đặc demo lon 380 ml (synthetic)", 380m, 2, 3 },
                    { 3, true, 140000m, null, 1, true, 3, null, "INDEPENDENT", null, null, 5, "Cà phê hạt", 1m, 3, 2 },
                    { 4, true, 250000m, null, 8, true, 4, null, "INDEPENDENT", null, null, 6, "Syrup Torani", 750m, 4, 3 },
                    { 5, true, 95000m, null, 10, true, 2, null, "INDEPENDENT", null, null, 12, "Kem béo Rich", 1m, 2, 4 },
                    { 6, true, 450000m, null, 9, true, 5, null, "INDEPENDENT", null, null, 1, "Matcha Nhật", 500m, 5, 1 },
                    { 7, true, 180000m, null, 5, true, 3, null, "INDEPENDENT", null, null, 2, "Bột cacao", 1m, 3, 2 },
                    { 8, true, 85000m, null, 4, true, 2, null, "INDEPENDENT", null, null, 2, "Bột sữa", 1m, 1, 2 },
                    { 9, true, 120000m, null, 3, true, 5, null, "INDEPENDENT", null, null, 1, "Trà đen demo 100 túi × 2 g (synthetic)", 200m, 4, 1 }
                });

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
                columns: new[] { "RecipeId", "Active", "DrinkId", "EffectiveDate", "Name", "OutputQuantity", "OutputUnitId", "ParentVersionId", "PreparedItemId", "RecipeCode", "SizeId", "Status", "ToppingId", "YieldPercentage", "YieldVarianceTolerancePercent" },
                values: new object[,]
                {
                    { 1, true, 1, null, "Recipe CF Sữa", null, null, null, null, "RCP_CF_SUA", 1, "Active", null, 100m, null },
                    { 2, true, 2, null, "Recipe CF Đen", null, null, null, null, "RCP_CF_DEN", 1, "Active", null, 100m, null },
                    { 3, true, 3, null, "Recipe Trà sữa", null, null, null, null, "RCP_TS", 1, "Active", null, 100m, null },
                    { 4, true, 4, null, "Recipe Trà sữa socola", null, null, null, null, "RCP_TS_SOCOLA", 1, "Active", null, 100m, null }
                });

            migrationBuilder.InsertData(
                table: "StaffAddresses",
                columns: new[] { "StaffAddressId", "Address", "IsDefault", "ProvinceId", "StaffId", "WardId" },
                values: new object[,]
                {
                    { 1, "123 Đường Nguyễn Huệ, Q1, TP.HCM", true, null, 1, null },
                    { 2, "456 Đường Lê Lợi, Q3, TP.HCM", true, null, 2, null },
                    { 3, "789 Đường Trần Hưng Đạo, Q5, TP.HCM", true, null, 3, null }
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
                    { 6, true, "0901000106", 6 },
                    { 15, true, "0901000115", 15 }
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
                    { 6, 1, 5, 5 },
                    { 15, 1, 5, 15 }
                });

            migrationBuilder.InsertData(
                table: "StaffShifts",
                columns: new[] { "StaffShiftId", "CustomEndTime", "CustomStartTime", "ShiftId", "StaffId", "StatusId", "WorkDate" },
                values: new object[,]
                {
                    { 1, null, null, 1, 4, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, null, null, 2, 5, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, null, null, 4, 6, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
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
                columns: new[] { "StoreInventoryId", "AvailableQty", "BtpIdentityState", "IngredientId", "LastUpdated", "MaxNegativeQty", "MinStockLevel", "PreparedItemId", "QuantitySemanticsEvidenceReference", "QuantitySemanticsEvidenceType", "QuantitySemanticsReviewedAt", "QuantitySemanticsReviewedByAccountId", "QuantitySemanticsStatus", "RecipeId", "StoreId", "SupersededByStoreInventoryId" },
                values: new object[,]
                {
                    { 1, 100m, null, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, null, null, null, null, 1, null },
                    { 2, 50m, null, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, null, null, null, null, 1, null },
                    { 3, 80m, null, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, null, null, null, null, 2, null },
                    { 4, 60m, null, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, null, null, null, null, null, 3, null }
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
                columns: new[] { "IngredientSupplierPriceHistoryId", "CreatedByStaffId", "EffectiveDate", "IngredientSupplierId", "IsCurrent", "Note", "PackageQuantity", "PackageUnitId", "Price" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, true, "Giá ban đầu", 1m, 2, 22000m },
                    { 2, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "Giá ban đầu", null, null, 27000m },
                    { 3, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "Giá ban đầu", 1m, 2, 140000m }
                });

            migrationBuilder.InsertData(
                table: "RecipeDetails",
                columns: new[] { "RecipeDetailId", "ChildRecipeId", "IngredientId", "Quantity", "RecipeId", "UnitId" },
                values: new object[,]
                {
                    { 1, null, 1, 50m, 1, 1 },
                    { 2, null, 2, 30m, 1, 3 },
                    { 3, null, 7, 100m, 1, 1 },
                    { 4, null, 1, 60m, 2, 1 },
                    { 5, null, 7, 100m, 2, 1 },
                    { 6, null, 3, 80m, 3, 1 },
                    { 7, null, 4, 40m, 3, 1 },
                    { 8, null, 6, 20m, 3, 1 },
                    { 9, null, 7, 100m, 3, 1 },
                    { 10, null, 3, 70m, 4, 1 },
                    { 11, null, 4, 40m, 4, 1 },
                    { 12, null, 5, 20m, 4, 1 },
                    { 13, null, 6, 20m, 4, 1 },
                    { 14, null, 7, 100m, 4, 1 },
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
                name: "IX_BranchReceiptLines_BaseUnitId",
                table: "BranchReceiptLines",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_BranchReceiptId",
                table: "BranchReceiptLines",
                column: "BranchReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_BranchReceiptId_SourceTransferCostAllocationId",
                table: "BranchReceiptLines",
                columns: new[] { "BranchReceiptId", "SourceTransferCostAllocationId" },
                unique: true,
                filter: "[SourceTransferCostAllocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_IngredientId",
                table: "BranchReceiptLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_IngredientSupplierId",
                table: "BranchReceiptLines",
                column: "IngredientSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_InputUnitId",
                table: "BranchReceiptLines",
                column: "InputUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_InventoryBaseUnitId",
                table: "BranchReceiptLines",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_InventoryTransactionId",
                table: "BranchReceiptLines",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_PackageUnitIdSnapshot",
                table: "BranchReceiptLines",
                column: "PackageUnitIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_PreparedItemId",
                table: "BranchReceiptLines",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_ProcurementUnitId",
                table: "BranchReceiptLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_PurchaseOrderLineId",
                table: "BranchReceiptLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_RecipeId",
                table: "BranchReceiptLines",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_RestockRequestFulfillmentId",
                table: "BranchReceiptLines",
                column: "RestockRequestFulfillmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_RestockRequestId",
                table: "BranchReceiptLines",
                column: "RestockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_SourceInventoryTransferDetailId",
                table: "BranchReceiptLines",
                column: "SourceInventoryTransferDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_SourceTransferCostAllocationId",
                table: "BranchReceiptLines",
                column: "SourceTransferCostAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceiptLines_SupplierId",
                table: "BranchReceiptLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_ConfirmedByStaffId",
                table: "BranchReceipts",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_CreatedByStaffId",
                table: "BranchReceipts",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_ReceivedByStaffId",
                table: "BranchReceipts",
                column: "ReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_SourceInventoryTransferId",
                table: "BranchReceipts",
                column: "SourceInventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_Status",
                table: "BranchReceipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_StoreId",
                table: "BranchReceipts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReceipts_SupplierId",
                table: "BranchReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_BranchReceipts_ActiveDraft_PurchaseOrder",
                table: "BranchReceipts",
                column: "PurchaseOrderId",
                unique: true,
                filter: "[PurchaseOrderId] IS NOT NULL AND [Status] = 'DRAFT'");

            migrationBuilder.CreateIndex(
                name: "UX_BranchReceipts_ReceiptCode",
                table: "BranchReceipts",
                column: "ReceiptCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BranchReceipts_Store_ReceiptKey",
                table: "BranchReceipts",
                columns: new[] { "StoreId", "ReceiptKey" },
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
                name: "IX_DocumentNumberCounters_CounterKey_DateKey",
                table: "DocumentNumberCounters",
                columns: new[] { "CounterKey", "DateKey" },
                unique: true);

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
                name: "IX_DrinkSizePriceAudits_DrinkSizeId_CreatedAtUtc",
                table: "DrinkSizePriceAudits",
                columns: new[] { "DrinkSizeId", "CreatedAtUtc" });

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
                name: "IX_DrinkSizeToppingPolicies_ToppingId",
                table: "DrinkSizeToppingPolicies",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "UX_DrinkSizeToppingPolicies_Active",
                table: "DrinkSizeToppingPolicies",
                columns: new[] { "DrinkSizeId", "ToppingId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkSizeToppingPolicyAudits_DrinkSizeToppingPolicyId_CreatedAtUtc",
                table: "DrinkSizeToppingPolicyAudits",
                columns: new[] { "DrinkSizeToppingPolicyId", "CreatedAtUtc" });

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
                name: "IX_ForecastPoints_ForecastRunId_ForecastDate",
                table: "ForecastPoints",
                columns: new[] { "ForecastRunId", "ForecastDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastRuns_StoreId_SeriesType_EntityId_TrainingToExclusive_HorizonDays_ModelVersion",
                table: "ForecastRuns",
                columns: new[] { "StoreId", "SeriesType", "EntityId", "TrainingToExclusive", "HorizonDays", "ModelVersion" },
                unique: true,
                filter: "[EntityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ClosedByStaffId",
                table: "IceAllocations",
                column: "ClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_CreatedByStaffId",
                table: "IceAllocations",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_IcePolicyId",
                table: "IceAllocations",
                column: "IcePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_IngredientId",
                table: "IceAllocations",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_OpenedByStaffId",
                table: "IceAllocations",
                column: "OpenedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_OperationalShiftId_IngredientId",
                table: "IceAllocations",
                columns: new[] { "OperationalShiftId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_PublicId",
                table: "IceAllocations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReservationReference",
                table: "IceAllocations",
                column: "ReservationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReturnedByStaffId",
                table: "IceAllocations",
                column: "ReturnedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_ReturnReceivedByStaffId",
                table: "IceAllocations",
                column: "ReturnReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceAllocations_StoreInventoryId_Status",
                table: "IceAllocations",
                columns: new[] { "StoreInventoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_FromIceAllocationId_ToIceAllocationId",
                table: "IceCarryOvers",
                columns: new[] { "FromIceAllocationId", "ToIceAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_FromOperationalShiftId",
                table: "IceCarryOvers",
                column: "FromOperationalShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_HandedOverByStaffId",
                table: "IceCarryOvers",
                column: "HandedOverByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_PublicId",
                table: "IceCarryOvers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ReceivedByStaffId",
                table: "IceCarryOvers",
                column: "ReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ToIceAllocationId",
                table: "IceCarryOvers",
                column: "ToIceAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_IceCarryOvers_ToOperationalShiftId",
                table: "IceCarryOvers",
                column: "ToOperationalShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_ApprovedByStaffId",
                table: "IceInventoryPostings",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_IceAllocationId_Revision_PostingType",
                table: "IceInventoryPostings",
                columns: new[] { "IceAllocationId", "Revision", "PostingType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_IdempotencyKey",
                table: "IceInventoryPostings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceInventoryPostings_InventoryTransactionId",
                table: "IceInventoryPostings",
                column: "InventoryTransactionId",
                unique: true,
                filter: "[InventoryTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_DisplayUnitId",
                table: "IcePolicies",
                column: "DisplayUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_IngredientId",
                table: "IcePolicies",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_StoreId",
                table: "IcePolicies",
                column: "StoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_StoreId_IngredientId",
                table: "IcePolicies",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcePolicies_UpdatedByStaffId",
                table: "IcePolicies",
                column: "UpdatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_ApprovedByStaffId",
                table: "IceSupplementalIssues",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_IceAllocationId_Status",
                table: "IceSupplementalIssues",
                columns: new[] { "IceAllocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_PublicId",
                table: "IceSupplementalIssues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_RejectedByStaffId",
                table: "IceSupplementalIssues",
                column: "RejectedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IceSupplementalIssues_RequestedByStaffId",
                table: "IceSupplementalIssues",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAudits_ImportSessionId_CreatedAtUtc",
                table: "ImportAudits",
                columns: new[] { "ImportSessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportAudits_StaffId_CreatedAtUtc",
                table: "ImportAudits",
                columns: new[] { "StaffId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportGroups_ImportSessionId_SheetName",
                table: "ImportGroups",
                columns: new[] { "ImportSessionId", "SheetName" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportGroups_ImportSourceDocumentId",
                table: "ImportGroups",
                column: "ImportSourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportItems_ImportGroupId_SourceRow",
                table: "ImportItems",
                columns: new[] { "ImportGroupId", "SourceRow" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportItems_Status_Action",
                table: "ImportItems",
                columns: new[] { "Status", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_FileHash",
                table: "ImportSessions",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_Status_ExpiresAtUtc",
                table: "ImportSessions",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_UploadedByAccountId_CreatedAtUtc",
                table: "ImportSessions",
                columns: new[] { "UploadedByAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceDocuments_FileHash",
                table: "ImportSourceDocuments",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceDocuments_ImportSessionId_SortOrder",
                table: "ImportSourceDocuments",
                columns: new[] { "ImportSessionId", "SortOrder" },
                unique: true);

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
                name: "IX_IngredientSupplierPriceHistories_CreatedByStaffId",
                table: "IngredientSupplierPriceHistories",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_IngredientSupplierId",
                table: "IngredientSupplierPriceHistories",
                column: "IngredientSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_IngredientSupplierId_EffectiveDate",
                table: "IngredientSupplierPriceHistories",
                columns: new[] { "IngredientSupplierId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_IngredientSupplierId_IsCurrent",
                table: "IngredientSupplierPriceHistories",
                columns: new[] { "IngredientSupplierId", "IsCurrent" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPriceHistories_PackageUnitId",
                table: "IngredientSupplierPriceHistories",
                column: "PackageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_Active",
                table: "IngredientSuppliers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_IngredientId_SupplierId",
                table: "IngredientSuppliers",
                columns: new[] { "IngredientId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_LooseProcurementUnitId",
                table: "IngredientSuppliers",
                column: "LooseProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_SupplierId",
                table: "IngredientSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_UnitId",
                table: "IngredientSuppliers",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UX_IngredientSuppliers_PrimaryByIngredient",
                table: "IngredientSuppliers",
                column: "IngredientId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IntelligencePilotRuns_FeatureCode_StoreId_CompletedAtUtc",
                table: "IntelligencePilotRuns",
                columns: new[] { "FeatureCode", "StoreId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationLines_PreparedItemId",
                table: "InventoryConsolidationLines",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationLines_RunId",
                table: "InventoryConsolidationLines",
                column: "InventoryConsolidationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationLines_StoreInventoryId",
                table: "InventoryConsolidationLines",
                column: "StoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryConsolidationLines_Run_Inventory_Role",
                table: "InventoryConsolidationLines",
                columns: new[] { "InventoryConsolidationRunId", "StoreInventoryId", "LineRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_ApprovedByStaffId",
                table: "InventoryConsolidationRuns",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_ExecutedByStaffId",
                table: "InventoryConsolidationRuns",
                column: "ExecutedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_QueryContractVersion",
                table: "InventoryConsolidationRuns",
                column: "QueryContractVersion");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_RequestedByStaffId",
                table: "InventoryConsolidationRuns",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_Store_ManifestHash",
                table: "InventoryConsolidationRuns",
                columns: new[] { "StoreId", "ManifestHash" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConsolidationRuns_Store_Status_CompletedAt",
                table: "InventoryConsolidationRuns",
                columns: new[] { "StoreId", "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_InventoryConsolidationRuns_Store_RequestKey",
                table: "InventoryConsolidationRuns",
                columns: new[] { "StoreId", "RequestKey" },
                unique: true);

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
                name: "IX_InventoryCostGapSettlements_InboundInventoryCostLayerId",
                table: "InventoryCostGapSettlements",
                column: "InboundInventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostGapSettlements_InventoryNegativeCostGapId_InboundInventoryCostLayerId",
                table: "InventoryCostGapSettlements",
                columns: new[] { "InventoryNegativeCostGapId", "InboundInventoryCostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_CreatedAt",
                table: "InventoryCostLayers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_IngredientId",
                table: "InventoryCostLayers",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_PreparedItemId",
                table: "InventoryCostLayers",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SourceBranchReceiptLineId",
                table: "InventoryCostLayers",
                column: "SourceBranchReceiptLineId",
                unique: true,
                filter: "[SourceBranchReceiptLineId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SourceInventoryDocumentDetailId",
                table: "InventoryCostLayers",
                column: "SourceInventoryDocumentDetailId",
                unique: true,
                filter: "[SourceInventoryDocumentDetailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SourceOrderRefundId",
                table: "InventoryCostLayers",
                column: "SourceOrderRefundId",
                filter: "[SourceOrderRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SourceTransferCostAllocationId",
                table: "InventoryCostLayers",
                column: "SourceTransferCostAllocationId",
                filter: "[SourceTransferCostAllocationId] IS NOT NULL");

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
                name: "IX_InventoryCostLayers_StoreId_PreparedItemId",
                table: "InventoryCostLayers",
                columns: new[] { "StoreId", "PreparedItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_StoreId_PreparedItemId_RemainingQuantity",
                table: "InventoryCostLayers",
                columns: new[] { "StoreId", "PreparedItemId", "RemainingQuantity" });

            migrationBuilder.CreateIndex(
                name: "UX_InventoryCostLayers_SourceProductionRunId",
                table: "InventoryCostLayers",
                column: "SourceProductionRunId",
                unique: true,
                filter: "[SourceProductionRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryCostLayers_TransferReturnPosting",
                table: "InventoryCostLayers",
                column: "SourceTransferDiscrepancyPostingId",
                unique: true,
                filter: "[SourceTransferDiscrepancyPostingId] IS NOT NULL");

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
                column: "InventoryDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItemSourceCapabilities_Ingredient",
                table: "InventoryItemSourceCapabilities",
                column: "IngredientId",
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItemSourceCapabilities_PreparedItem",
                table: "InventoryItemSourceCapabilities",
                column: "PreparedItemId",
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovalLines_InventoryNegativeApprovalId_InventoryDocumentDetailId",
                table: "InventoryNegativeApprovalLines",
                columns: new[] { "InventoryNegativeApprovalId", "InventoryDocumentDetailId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_ApproverStaffId",
                table: "InventoryNegativeApprovals",
                column: "ApproverStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_InventoryDocumentId",
                table: "InventoryNegativeApprovals",
                column: "InventoryDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_RequesterStaffId",
                table: "InventoryNegativeApprovals",
                column: "RequesterStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_RequestKey_RequesterStaffId",
                table: "InventoryNegativeApprovals",
                columns: new[] { "RequestKey", "RequesterStaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_Status_RequestedAt",
                table: "InventoryNegativeApprovals",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeApprovals_StoreId",
                table: "InventoryNegativeApprovals",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeCostGaps_InventoryDocumentDetailId",
                table: "InventoryNegativeCostGaps",
                column: "InventoryDocumentDetailId",
                unique: true,
                filter: "[InventoryDocumentDetailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeCostGaps_InventoryNegativeApprovalId",
                table: "InventoryNegativeCostGaps",
                column: "InventoryNegativeApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeCostGaps_InventoryTransactionId",
                table: "InventoryNegativeCostGaps",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeCostGaps_SalesCostGapId",
                table: "InventoryNegativeCostGaps",
                column: "SalesCostGapId",
                unique: true,
                filter: "[SalesCostGapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryNegativeCostGaps_StoreInventoryId_Status_OccurredAt",
                table: "InventoryNegativeCostGaps",
                columns: new[] { "StoreInventoryId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BranchReceiptLineId",
                table: "InventoryTransactions",
                column: "BranchReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CreatedAt",
                table: "InventoryTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryConsolidationRunId",
                table: "InventoryTransactions",
                column: "InventoryConsolidationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryDocumentId",
                table: "InventoryTransactions",
                column: "InventoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryTransferDetailId",
                table: "InventoryTransactions",
                column: "InventoryTransferDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryTransferId",
                table: "InventoryTransactions",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OrderRefundId",
                table: "InventoryTransactions",
                column: "OrderRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductionRunId",
                table: "InventoryTransactions",
                column: "ProductionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceOrderId",
                table: "InventoryTransactions",
                column: "ReferenceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SourceRecipeId",
                table: "InventoryTransactions",
                column: "SourceRecipeId");

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
                name: "UX_InventoryTransactions_BranchReceiptLine_Type",
                table: "InventoryTransactions",
                columns: new[] { "BranchReceiptLineId", "Type" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_ConsolidationRun_Inventory_Type",
                table: "InventoryTransactions",
                columns: new[] { "InventoryConsolidationRunId", "StoreInventoryId", "Type" },
                unique: true,
                filter: "[InventoryConsolidationRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_DocumentDetail_Type",
                table: "InventoryTransactions",
                columns: new[] { "InventoryDocumentDetailId", "Type" },
                unique: true,
                filter: "[InventoryDocumentDetailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_OrderRefund_Inventory_Type",
                table: "InventoryTransactions",
                columns: new[] { "OrderRefundId", "StoreInventoryId", "Type" },
                unique: true,
                filter: "[OrderRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_ProductionRun_Inventory_Type",
                table: "InventoryTransactions",
                columns: new[] { "ProductionRunId", "StoreInventoryId", "Type" },
                unique: true,
                filter: "[ProductionRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_TransferDetail_Type",
                table: "InventoryTransactions",
                columns: new[] { "InventoryTransferDetailId", "Type" },
                unique: true,
                filter: "[InventoryTransferDetailId] IS NOT NULL AND [Type] = 10");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferCostAllocations_InventoryTransferDetailId_SourceInventoryCostLayerId",
                table: "InventoryTransferCostAllocations",
                columns: new[] { "InventoryTransferDetailId", "SourceInventoryCostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferCostAllocations_SourceInventoryCostLayerId",
                table: "InventoryTransferCostAllocations",
                column: "SourceInventoryCostLayerId");

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
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_InventoryTransferId_PreparedItemId",
                table: "InventoryTransferDetails",
                columns: new[] { "InventoryTransferId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_ParentInventoryTransferDetailId",
                table: "InventoryTransferDetails",
                column: "ParentInventoryTransferDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_PreparedItemId",
                table: "InventoryTransferDetails",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_RestockRequestFulfillmentId",
                table: "InventoryTransferDetails",
                column: "RestockRequestFulfillmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_RestockRequestId",
                table: "InventoryTransferDetails",
                column: "RestockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDetails_UnitId",
                table: "InventoryTransferDetails",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyPostings_ActorStaffId",
                table: "InventoryTransferDiscrepancyPostings",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyPostings_InventoryTransferCostAllocationId",
                table: "InventoryTransferDiscrepancyPostings",
                column: "InventoryTransferCostAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyPostings_InventoryTransferDetailId_PostingType",
                table: "InventoryTransferDiscrepancyPostings",
                columns: new[] { "InventoryTransferDetailId", "PostingType" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyPostings_RelatedPostingId",
                table: "InventoryTransferDiscrepancyPostings",
                column: "RelatedPostingId");

            migrationBuilder.CreateIndex(
                name: "UX_TransferDiscrepancyPosting_Request_Line_Type_Cost",
                table: "InventoryTransferDiscrepancyPostings",
                columns: new[] { "RequestKey", "InventoryTransferDetailId", "PostingType", "InventoryTransferCostAllocationId", "RelatedPostingId" },
                unique: true);

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
                name: "IX_InventoryTransfers_ParentInventoryTransferId",
                table: "InventoryTransfers",
                column: "ParentInventoryTransferId");

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
                name: "IX_InventoryWriterModeTransitions_ActorAccountId",
                table: "InventoryWriterModeTransitions",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWriterModeTransitions_StoreId_RequestedAt",
                table: "InventoryWriterModeTransitions",
                columns: new[] { "StoreId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWriterModeTransitions_Succeeded",
                table: "InventoryWriterModeTransitions",
                column: "Succeeded");

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
                name: "IX_OperationalAnomalies_AcknowledgedByStaffId",
                table: "OperationalAnomalies",
                column: "AcknowledgedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAnomalies_FeedbackByStaffId",
                table: "OperationalAnomalies",
                column: "FeedbackByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAnomalies_ResolvedByStaffId",
                table: "OperationalAnomalies",
                column: "ResolvedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAnomalies_StoreId_BusinessDate_MetricCode_DetectionVersion",
                table: "OperationalAnomalies",
                columns: new[] { "StoreId", "BusinessDate", "MetricCode", "DetectionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAnomalies_StoreId_Status_Severity",
                table: "OperationalAnomalies",
                columns: new[] { "StoreId", "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_ClosedByStaffId",
                table: "OperationalShifts",
                column: "ClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_CreatedByStaffId",
                table: "OperationalShifts",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_OpenedByStaffId",
                table: "OperationalShifts",
                column: "OpenedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_ShiftLeadId",
                table: "OperationalShifts",
                column: "ShiftLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_SourceScheduleShiftId",
                table: "OperationalShifts",
                column: "SourceScheduleShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_CreationSource",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "CreationSource" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Name",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Name" },
                unique: true,
                filter: "[CreationSource] = 'Manual' AND [Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_SourceScheduleShiftId",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "SourceScheduleShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShifts_StoreId_BusinessDate_Status",
                table: "OperationalShifts",
                columns: new[] { "StoreId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftScheduleSources_StaffShiftId",
                table: "OperationalShiftScheduleSources",
                column: "StaffShiftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftWorkShifts_LinkedByStaffId",
                table: "OperationalShiftWorkShifts",
                column: "LinkedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalShiftWorkShifts_WorkShiftId",
                table: "OperationalShiftWorkShifts",
                column: "WorkShiftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_DrinkId",
                table: "OrderDetails",
                column: "DrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_DrinkSizeId",
                table: "OrderDetails",
                column: "DrinkSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_IceIngredientId",
                table: "OrderDetails",
                column: "IceIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrderId",
                table: "OrderDetails",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_RecipeIdSnapshot",
                table: "OrderDetails",
                column: "RecipeIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_SizeId",
                table: "OrderDetails",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_StoreMenuItemId",
                table: "OrderDetails",
                column: "StoreMenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderRefunds_CompletedByStaffId",
                table: "OrderRefunds",
                column: "CompletedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderRefunds_RequestedByStaffId",
                table: "OrderRefunds",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderRefunds_Order_ActiveOrCompleted",
                table: "OrderRefunds",
                column: "OrderId",
                unique: true,
                filter: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "UX_OrderRefunds_Store_RefundKey",
                table: "OrderRefunds",
                columns: new[] { "StoreId", "RefundKey" },
                unique: true);

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
                name: "IX_Orders_RecommendationSessionId",
                table: "Orders",
                column: "RecommendationSessionId",
                filter: "[RecommendationSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StaffId",
                table: "Orders",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId",
                table: "Orders",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TerminalId",
                table: "Orders",
                column: "TerminalId");

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
                name: "IX_OrderToppings_RecipeIdSnapshot",
                table: "OrderToppings",
                column: "RecipeIdSnapshot");

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
                name: "IX_OtpChallenges_ClientIpHash_CreatedAt",
                table: "OtpChallenges",
                columns: new[] { "ClientIpHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_ConfirmedByStaffId",
                table: "OtpChallenges",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_CreatedAt",
                table: "OtpChallenges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_DeviceFingerprintHash_CreatedAt",
                table: "OtpChallenges",
                columns: new[] { "DeviceFingerprintHash", "CreatedAt" });

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
                name: "IX_OtpChallenges_RequestKey",
                table: "OtpChallenges",
                column: "RequestKey");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_StoreId_RequestedByStaffId_ActionType_TargetType_TargetId_Status",
                table: "OtpChallenges",
                columns: new[] { "StoreId", "RequestedByStaffId", "ActionType", "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_StoreId_Status_ExpiresAt",
                table: "OtpChallenges",
                columns: new[] { "StoreId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_TerminalId",
                table: "OtpChallenges",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_WorkShiftId",
                table: "OtpChallenges",
                column: "WorkShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_OtpChallenges_OneActivePerActorActionTarget",
                table: "OtpChallenges",
                columns: new[] { "StoreId", "RequestedByStaffId", "ActionType", "TargetType", "TargetId" },
                unique: true,
                filter: "[Status] IN ('Pending', 'Approved')");

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
                name: "IX_Payments_PaidByStaffId",
                table: "Payments",
                column: "PaidByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentMethodId",
                table: "Payments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentStatusId",
                table: "Payments",
                column: "PaymentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StoreId",
                table: "Payments",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TerminalId",
                table: "Payments",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WorkShiftId",
                table: "Payments",
                column: "WorkShiftId");

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
                name: "IX_PosAccessSessions_AccountId",
                table: "PosAccessSessions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_EndedByStaffId",
                table: "PosAccessSessions",
                column: "EndedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_JwtId",
                table: "PosAccessSessions",
                column: "JwtId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_PublicId",
                table: "PosAccessSessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_StaffId",
                table: "PosAccessSessions",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_StoreId_Status_ExpiresAtUtc",
                table: "PosAccessSessions",
                columns: new[] { "StoreId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosAccessSessions_WorkShiftId",
                table: "PosAccessSessions",
                column: "WorkShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_PosAccessSessions_ActiveTerminal",
                table: "PosAccessSessions",
                column: "TerminalId",
                unique: true,
                filter: "[Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_PosCatalogStates_StoreId",
                table: "PosCatalogStates",
                column: "StoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationCatalog_RecommendedDrinkId",
                table: "PosRecommendationCatalog",
                column: "RecommendedDrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationCatalog_StoreId_TriggerDrinkId_Rank_ExpiresAtUtc",
                table: "PosRecommendationCatalog",
                columns: new[] { "StoreId", "TriggerDrinkId", "Rank", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationCatalog_StoreId_TriggerDrinkId_RecommendedDrinkId_ModelVersion",
                table: "PosRecommendationCatalog",
                columns: new[] { "StoreId", "TriggerDrinkId", "RecommendedDrinkId", "ModelVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationCatalog_TriggerDrinkId",
                table: "PosRecommendationCatalog",
                column: "TriggerDrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationExposureItems_PosRecommendationExposureId_TriggerDrinkId_RecommendedDrinkId",
                table: "PosRecommendationExposureItems",
                columns: new[] { "PosRecommendationExposureId", "TriggerDrinkId", "RecommendedDrinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationExposures_OrderId",
                table: "PosRecommendationExposures",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationExposures_RecommendationSessionId",
                table: "PosRecommendationExposures",
                column: "RecommendationSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosRecommendationExposures_StoreId_CreatedAtUtc",
                table: "PosRecommendationExposures",
                columns: new[] { "StoreId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminals_StoreId",
                table: "PosTerminals",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedItems_Active",
                table: "PreparedItems",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedItems_BaseUnitId",
                table: "PreparedItems",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparedItems_Code",
                table: "PreparedItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCostAllocations_InventoryCostLayerId",
                table: "ProductionCostAllocations",
                column: "InventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCostAllocations_InventoryTransactionId",
                table: "ProductionCostAllocations",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCostAllocations_ProductionRunId",
                table: "ProductionCostAllocations",
                column: "ProductionRunId");

            migrationBuilder.CreateIndex(
                name: "UX_ProductionCostAllocations_Run_Layer",
                table: "ProductionCostAllocations",
                columns: new[] { "ProductionRunId", "InventoryCostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProductionCostAllocations_Run_Tx_Layer",
                table: "ProductionCostAllocations",
                columns: new[] { "ProductionRunId", "InventoryTransactionId", "InventoryCostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_BaseUnitId",
                table: "ProductionRunInputActuals",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ConfirmedByStaffId",
                table: "ProductionRunInputActuals",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_IngredientId",
                table: "ProductionRunInputActuals",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_PreparedItemId",
                table: "ProductionRunInputActuals",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ProductionRunId_IngredientId",
                table: "ProductionRunInputActuals",
                columns: new[] { "ProductionRunId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunInputActuals_ProductionRunId_PreparedItemId",
                table: "ProductionRunInputActuals",
                columns: new[] { "ProductionRunId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_BaseUnitId",
                table: "ProductionRunOutputs",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_ProductionRunId",
                table: "ProductionRunOutputs",
                column: "ProductionRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunOutputs_RecordedByStaffId",
                table: "ProductionRunOutputs",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_CompletedByStaffId",
                table: "ProductionRuns",
                column: "CompletedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_CreatedByStaffId",
                table: "ProductionRuns",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_OutputBaseUnitId",
                table: "ProductionRuns",
                column: "OutputBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_RecipeId",
                table: "ProductionRuns",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_Store_CreatedAt",
                table: "ProductionRuns",
                columns: new[] { "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ProductionRuns_Store_RequestKey",
                table: "ProductionRuns",
                columns: new[] { "StoreId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunTransitions_ActorStaffId",
                table: "ProductionRunTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRunTransitions_ProductionRunId_OccurredAtUtc",
                table: "ProductionRunTransitions",
                columns: new[] { "ProductionRunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_Code",
                table: "ProductTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Code",
                table: "Provinces",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_CountryId",
                table: "Provinces",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_ActorStaffId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BaseUnitId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "BranchReceiptLineId", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NOT NULL AND [PostingType] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_BranchReceiptLineId_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "BranchReceiptLineId", "PurchaseOrderLineId", "PostingType" },
                unique: true,
                filter: "[BranchReceiptLineId] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NULL AND [PostingType] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "CloseOperationKey", "PurchaseOrderLineAllocationId", "PostingType" },
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NOT NULL AND [PostingType] = 'CLOSED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_CloseOperationKey_PurchaseOrderLineId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "CloseOperationKey", "PurchaseOrderLineId", "PostingType" },
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL AND [PurchaseOrderLineAllocationId] IS NULL AND [PostingType] = 'CLOSED'");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_PurchaseAdviceLineId_CreatedAtUtc",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "PurchaseAdviceLineId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_PurchaseOrderLineAllocationId_PostingType",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "PurchaseOrderLineAllocationId", "PostingType" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_PurchaseOrderLineId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceFulfillmentPostings_SourceDocumentType_SourceDocumentId_SourceDocumentLineId_PostingType_PurchaseAdviceLineId",
                table: "PurchaseAdviceFulfillmentPostings",
                columns: new[] { "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId", "PostingType", "PurchaseAdviceLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_BaseUnitId",
                table: "PurchaseAdviceLines",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_IngredientId",
                table: "PurchaseAdviceLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_ProcurementUnitId",
                table: "PurchaseAdviceLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_PurchaseAdviceId",
                table: "PurchaseAdviceLines",
                column: "PurchaseAdviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceLines_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines",
                column: "RestockSourcingAllocationId");

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseAdviceLines_ActiveRestock",
                table: "PurchaseAdviceLines",
                column: "RestockRequestId",
                unique: true,
                filter: "[IsActiveReservation] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_AdviceNumber",
                table: "PurchaseAdvices",
                column: "AdviceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_CancelledByStaffId",
                table: "PurchaseAdvices",
                column: "CancelledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_RejectedByStaffId",
                table: "PurchaseAdvices",
                column: "RejectedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_RequestedByStaffId",
                table: "PurchaseAdvices",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_RequestKey",
                table: "PurchaseAdvices",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_ReviewedByStaffId",
                table: "PurchaseAdvices",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdvices_StoreId_Status_CreatedAtUtc",
                table: "PurchaseAdvices",
                columns: new[] { "StoreId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceTransitions_ActorStaffId",
                table: "PurchaseAdviceTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseAdviceTransitions_PurchaseAdviceId_OccurredAtUtc",
                table: "PurchaseAdviceTransitions",
                columns: new[] { "PurchaseAdviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_GeneratedByStaffId",
                table: "PurchaseOrderBatchDocumentRevisions",
                column: "GeneratedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatchId_ContentHash",
                table: "PurchaseOrderBatchDocumentRevisions",
                columns: new[] { "PurchaseOrderBatchId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatchId_RevisionNumber",
                table: "PurchaseOrderBatchDocumentRevisions",
                columns: new[] { "PurchaseOrderBatchId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatchId_SentIdempotencyKey",
                table: "PurchaseOrderBatchDocumentRevisions",
                columns: new[] { "PurchaseOrderBatchId", "SentIdempotencyKey" },
                unique: true,
                filter: "[SentIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_PurchaseOrderBatchId_Status",
                table: "PurchaseOrderBatchDocumentRevisions",
                columns: new[] { "PurchaseOrderBatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_SentByStaffId",
                table: "PurchaseOrderBatchDocumentRevisions",
                column: "SentByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchDocumentRevisions_SupersededByRevisionId",
                table: "PurchaseOrderBatchDocumentRevisions",
                column: "SupersededByRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_ApprovedByStaffId",
                table: "PurchaseOrderBatches",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_BatchNumber",
                table: "PurchaseOrderBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_CancelledByStaffId",
                table: "PurchaseOrderBatches",
                column: "CancelledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_CreatedByStaffId",
                table: "PurchaseOrderBatches",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_RequestKey",
                table: "PurchaseOrderBatches",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatches_SupplierId_Status_CreatedAtUtc",
                table: "PurchaseOrderBatches",
                columns: new[] { "SupplierId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_IngredientId",
                table: "PurchaseOrderBatchLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_IngredientSupplierId",
                table: "PurchaseOrderBatchLines",
                column: "IngredientSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_PackageUnitId",
                table: "PurchaseOrderBatchLines",
                column: "PackageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_ProcurementUnitId",
                table: "PurchaseOrderBatchLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderBatchLines_PurchaseOrderBatchId_IngredientId",
                table: "PurchaseOrderBatchLines",
                columns: new[] { "PurchaseOrderBatchId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_ProcurementUnitId",
                table: "PurchaseOrderLineAllocations",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_PurchaseAdviceLineId",
                table: "PurchaseOrderLineAllocations",
                column: "PurchaseAdviceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_PurchaseOrderBatchLineId",
                table: "PurchaseOrderLineAllocations",
                column: "PurchaseOrderBatchLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_PurchaseOrderId",
                table: "PurchaseOrderLineAllocations",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineAllocations_PurchaseOrderLineId",
                table: "PurchaseOrderLineAllocations",
                column: "PurchaseOrderLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_ActorStaffId",
                table: "PurchaseOrderLineClosures",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_ProcurementUnitId",
                table: "PurchaseOrderLineClosures",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_PurchaseOrderLineId_CreatedAtUtc",
                table: "PurchaseOrderLineClosures",
                columns: new[] { "PurchaseOrderLineId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineClosures_RequestKey",
                table: "PurchaseOrderLineClosures",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ClosedRemainingByStaffId",
                table: "PurchaseOrderLines",
                column: "ClosedRemainingByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_IngredientId",
                table: "PurchaseOrderLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_IngredientSupplierId",
                table: "PurchaseOrderLines",
                column: "IngredientSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_InventoryBaseUnitId",
                table: "PurchaseOrderLines",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PackageUnitIdSnapshot",
                table: "PurchaseOrderLines",
                column: "PackageUnitIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ProcurementUnitId",
                table: "PurchaseOrderLines",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseAdviceLineId",
                table: "PurchaseOrderLines",
                column: "PurchaseAdviceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId",
                table: "PurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_RestockRequestId",
                table: "PurchaseOrderLines",
                column: "RestockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_BranchReceiptLineId",
                table: "PurchaseOrderReceiptPostings",
                column: "BranchReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_CreatedByStaffId",
                table: "PurchaseOrderReceiptPostings",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_InventoryBaseUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "InventoryBaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_ProcurementUnitId",
                table: "PurchaseOrderReceiptPostings",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptPostings_PurchaseOrderLineId",
                table: "PurchaseOrderReceiptPostings",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedByStaffId",
                table: "PurchaseOrders",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Code",
                table: "PurchaseOrders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedByStaffId",
                table: "PurchaseOrders",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_MasterPurchaseOrderId",
                table: "PurchaseOrders",
                column: "MasterPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaseOrderBatchId",
                table: "PurchaseOrders",
                column: "PurchaseOrderBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SentByStaffId",
                table: "PurchaseOrders",
                column: "SentByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_StoreId_Status",
                table: "PurchaseOrders",
                columns: new[] { "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

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
                name: "IX_Recipes_OneActive_PreparedItem",
                table: "Recipes",
                column: "PreparedItemId",
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL AND [Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputUnitId",
                table: "Recipes",
                column: "OutputUnitId");

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
                name: "UX_Recipes_OneActive_Drink_Size",
                table: "Recipes",
                columns: new[] { "DrinkId", "SizeId" },
                unique: true,
                filter: "[DrinkId] IS NOT NULL AND [SizeId] IS NOT NULL AND [ToppingId] IS NULL AND [Active] = 1 AND [Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_RefundCostGaps_OrderRefundId",
                table: "RefundCostGaps",
                column: "OrderRefundId");

            migrationBuilder.CreateIndex(
                name: "UX_RefundCostGaps_SalesCostGapId",
                table: "RefundCostGaps",
                column: "SalesCostGapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundCostReversals_InventoryTransactionId",
                table: "RefundCostReversals",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundCostReversals_OrderRefundId",
                table: "RefundCostReversals",
                column: "OrderRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundCostReversals_OriginalInventoryCostLayerId",
                table: "RefundCostReversals",
                column: "OriginalInventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundCostReversals_ReturnInventoryCostLayerId",
                table: "RefundCostReversals",
                column: "ReturnInventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "UX_RefundCostReversals_SalesCostAllocationId",
                table: "RefundCostReversals",
                column: "SalesCostAllocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestDeduplications_AccountId",
                table: "RequestDeduplications",
                column: "AccountId");

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
                name: "IX_RequestDeduplications_RequestKey_ActionName_StaffId_StoreId",
                table: "RequestDeduplications",
                columns: new[] { "RequestKey", "ActionName", "StaffId", "StoreId" },
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
                name: "IX_RequestDeduplications_StoreId",
                table: "RequestDeduplications",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockFulfillmentPostings_BaseUnitId",
                table: "RestockFulfillmentPostings",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockFulfillmentPostings_IngredientId",
                table: "RestockFulfillmentPostings",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockFulfillmentPostings_PreparedItemId",
                table: "RestockFulfillmentPostings",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockFulfillmentPostings_RestockRequestId_CreatedAtUtc",
                table: "RestockFulfillmentPostings",
                columns: new[] { "RestockRequestId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RestockFulfillmentPosting_SourceLine_Request",
                table: "RestockFulfillmentPostings",
                columns: new[] { "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId", "RestockRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestFulfillments_CreatedByStaffId",
                table: "RestockRequestFulfillments",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestFulfillments_RestockRequestId",
                table: "RestockRequestFulfillments",
                column: "RestockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestFulfillments_SourceType",
                table: "RestockRequestFulfillments",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestFulfillments_Status",
                table: "RestockRequestFulfillments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_AcceptedByStaffId",
                table: "RestockRequests",
                column: "AcceptedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_CreatedByStaffId",
                table: "RestockRequests",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_CreatedForStoreId",
                table: "RestockRequests",
                column: "CreatedForStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_HandledByStaffId",
                table: "RestockRequests",
                column: "HandledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_IngredientId",
                table: "RestockRequests",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_PreparedItemId",
                table: "RestockRequests",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_ProcurementUnitId",
                table: "RestockRequests",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_RecipeId",
                table: "RestockRequests",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_RemainingClosedByStaffId",
                table: "RestockRequests",
                column: "RemainingClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_Status",
                table: "RestockRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_StoreId",
                table: "RestockRequests",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_StoreId_SourceType_Status",
                table: "RestockRequests",
                columns: new[] { "StoreId", "SourceType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequests_StoreId_SourcingStatus",
                table: "RestockRequests",
                columns: new[] { "StoreId", "SourcingStatus" });

            migrationBuilder.CreateIndex(
                name: "UX_RestockRequest_Active_StockAlert",
                table: "RestockRequests",
                column: "StockAlertId",
                unique: true,
                filter: "[StockAlertId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')");

            migrationBuilder.CreateIndex(
                name: "UX_RestockRequest_Active_Store_Ingredient",
                table: "RestockRequests",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')");

            migrationBuilder.CreateIndex(
                name: "UX_RestockRequest_Active_Store_PreparedItem",
                table: "RestockRequests",
                columns: new[] { "StoreId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')");

            migrationBuilder.CreateIndex(
                name: "UX_RestockRequests_ReferenceCode",
                table: "RestockRequests",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_ActorStaffId",
                table: "RestockRequestTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_BranchReceiptId",
                table: "RestockRequestTransitions",
                column: "BranchReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_InventoryTransactionId",
                table: "RestockRequestTransitions",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_InventoryTransferId",
                table: "RestockRequestTransitions",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_OccurredAtUtc",
                table: "RestockRequestTransitions",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_RestockRequestId",
                table: "RestockRequestTransitions",
                column: "RestockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRequestTransitions_RestockRequestId_OccurredAtUtc",
                table: "RestockRequestTransitions",
                columns: new[] { "RestockRequestId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_CreatedByStaffId",
                table: "RestockSourcingAllocations",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_InventoryTransferId",
                table: "RestockSourcingAllocations",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ProcurementUnitId",
                table: "RestockSourcingAllocations",
                column: "ProcurementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseAdviceLineId",
                filter: "[PurchaseAdviceLineId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_PurchaseOrderLineId",
                table: "RestockSourcingAllocations",
                column: "PurchaseOrderLineId",
                unique: true,
                filter: "[PurchaseOrderLineId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_ReleasedByStaffId",
                table: "RestockSourcingAllocations",
                column: "ReleasedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_DecisionType_Status",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "DecisionType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_SourceDocumentType_SourceDocumentId_SourceDocumentLineId",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId" },
                unique: true,
                filter: "[SourceDocumentType] IS NOT NULL AND [SourceDocumentId] IS NOT NULL AND [Status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_RestockSourcingAllocations_RestockRequestId_Status",
                table: "RestockSourcingAllocations",
                columns: new[] { "RestockRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_RestockSourcingAllocations_ProductionRun",
                table: "RestockSourcingAllocations",
                column: "ProductionRunId",
                unique: true,
                filter: "[ProductionRunId] IS NOT NULL");

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
                name: "IX_SalesCostAllocations_InventoryCostLayerId",
                table: "SalesCostAllocations",
                column: "InventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostAllocations_InventoryTransactionId",
                table: "SalesCostAllocations",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostAllocations_OrderDetailId",
                table: "SalesCostAllocations",
                column: "OrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostAllocations_OrderId",
                table: "SalesCostAllocations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostAllocations_OrderToppingId",
                table: "SalesCostAllocations",
                column: "OrderToppingId");

            migrationBuilder.CreateIndex(
                name: "UX_SalesCostAllocations_Order_Line_Tx_Layer",
                table: "SalesCostAllocations",
                columns: new[] { "OrderId", "OrderDetailId", "OrderToppingId", "InventoryTransactionId", "InventoryCostLayerId" },
                unique: true,
                filter: "[OrderToppingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostGaps_OrderDetailId",
                table: "SalesCostGaps",
                column: "OrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostGaps_OrderId",
                table: "SalesCostGaps",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCostGaps_OrderToppingId",
                table: "SalesCostGaps",
                column: "OrderToppingId");

            migrationBuilder.CreateIndex(
                name: "UX_SalesCostGaps_Order_Line_Identity",
                table: "SalesCostGaps",
                columns: new[] { "OrderId", "OrderDetailId", "OrderToppingId", "IngredientId", "PreparedItemId" },
                unique: true,
                filter: "[OrderToppingId] IS NOT NULL AND [IngredientId] IS NOT NULL AND [PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationAssignments_ScheduleOptimizationProposalId_StaffId_ShiftId_WorkDate",
                table: "ScheduleOptimizationAssignments",
                columns: new[] { "ScheduleOptimizationProposalId", "StaffId", "ShiftId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationAssignments_ShiftId",
                table: "ScheduleOptimizationAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationAssignments_StaffId",
                table: "ScheduleOptimizationAssignments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationProposals_CreatedByStaffId",
                table: "ScheduleOptimizationProposals",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationProposals_ForecastRunId",
                table: "ScheduleOptimizationProposals",
                column: "ForecastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOptimizationProposals_StoreId_FromDate_ToDate_CreatedAtUtc",
                table: "ScheduleOptimizationProposals",
                columns: new[] { "StoreId", "FromDate", "ToDate", "CreatedAtUtc" });

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
                name: "IX_StaffAddresses_ProvinceId",
                table: "StaffAddresses",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAddresses_StaffId",
                table: "StaffAddresses",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAddresses_WardId",
                table: "StaffAddresses",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAvailabilityExceptions_CreatedByStaffId",
                table: "StaffAvailabilityExceptions",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAvailabilityExceptions_StaffId_Date",
                table: "StaffAvailabilityExceptions",
                columns: new[] { "StaffId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAvailabilityRules_CreatedByStaffId",
                table: "StaffAvailabilityRules",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAvailabilityRules_StaffId_DayOfWeek_EffectiveFrom",
                table: "StaffAvailabilityRules",
                columns: new[] { "StaffId", "DayOfWeek", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotification_Entity",
                table: "StaffNotifications",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotification_OtpChallengeId",
                table: "StaffNotifications",
                column: "OtpChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotification_Recipient_IsRead",
                table: "StaffNotifications",
                columns: new[] { "RecipientStaffId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotification_StoreId",
                table: "StaffNotifications",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UX_StaffNotification_DeduplicationKey",
                table: "StaffNotifications",
                column: "DeduplicationKey",
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

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
                name: "IX_StaffTimeOffs_RequestedByStaffId",
                table: "StaffTimeOffs",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTimeOffs_ReviewedByStaffId",
                table: "StaffTimeOffs",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTimeOffs_StaffId_FromUtc_ToUtc",
                table: "StaffTimeOffs",
                columns: new[] { "StaffId", "FromUtc", "ToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkConstraints_CreatedByStaffId",
                table: "StaffWorkConstraints",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkConstraints_StaffId_EffectiveFrom",
                table: "StaffWorkConstraints",
                columns: new[] { "StaffId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_ConfirmedByStaffId",
                table: "StockAlerts",
                column: "ConfirmedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_IngredientId",
                table: "StockAlerts",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_PreparedItemId",
                table: "StockAlerts",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_RecipeId",
                table: "StockAlerts",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_RejectedByStaffId",
                table: "StockAlerts",
                column: "RejectedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_ReportedByStaffId",
                table: "StockAlerts",
                column: "ReportedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_Status",
                table: "StockAlerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_StoreId",
                table: "StockAlerts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlerts_StoreId_RecipeId",
                table: "StockAlerts",
                columns: new[] { "StoreId", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "UX_StockAlert_Active_Store_Ingredient",
                table: "StockAlerts",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL AND [Status] IN ('OPEN','CONFIRMED')");

            migrationBuilder.CreateIndex(
                name: "UX_StockAlert_Active_Store_PreparedItem",
                table: "StockAlerts",
                columns: new[] { "StoreId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL AND [Status] IN ('OPEN','CONFIRMED')");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlertTransitions_ActorStaffId",
                table: "StockAlertTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAlertTransitions_SourceType_SourceId",
                table: "StockAlertTransitions",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlertTransitions_StockAlertId_CreatedAtUtc",
                table: "StockAlertTransitions",
                columns: new[] { "StockAlertId", "CreatedAtUtc" });

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
                name: "IX_Store_PreparedItem_Compatibility",
                table: "StoreInventories",
                columns: new[] { "StoreId", "PreparedItemId" },
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_IngredientId",
                table: "StoreInventories",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_PreparedItemId",
                table: "StoreInventories",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_QuantitySemanticsReviewedByAccountId",
                table: "StoreInventories",
                column: "QuantitySemanticsReviewedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_RecipeId",
                table: "StoreInventories",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_StoreId",
                table: "StoreInventories",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_SupersededByStoreInventoryId",
                table: "StoreInventories",
                column: "SupersededByStoreInventoryId");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Ingredient",
                table: "StoreInventories",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Store_PreparedItem_Canonical",
                table: "StoreInventories",
                columns: new[] { "PreparedItemId", "StoreId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL AND [BtpIdentityState] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Store_Recipe",
                table: "StoreInventories",
                columns: new[] { "StoreId", "RecipeId" },
                unique: true,
                filter: "[RecipeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItemAudits_ActorStaffId",
                table: "StoreMenuItemAudits",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItemAudits_StoreId_CreatedAtUtc",
                table: "StoreMenuItemAudits",
                columns: new[] { "StoreId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItemAudits_StoreMenuItemId_CreatedAtUtc",
                table: "StoreMenuItemAudits",
                columns: new[] { "StoreMenuItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItems_DrinkSizeId",
                table: "StoreMenuItems",
                column: "DrinkSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItems_PublishedByStaffId",
                table: "StoreMenuItems",
                column: "PublishedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItems_StoreId_DisplayOrder",
                table: "StoreMenuItems",
                columns: new[] { "StoreId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreMenuItems_StoreId_IsEnabled_EffectiveFromUtc_EffectiveToUtc",
                table: "StoreMenuItems",
                columns: new[] { "StoreId", "IsEnabled", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_StoreMenuItems_Store_DrinkSize",
                table: "StoreMenuItems",
                columns: new[] { "StoreId", "DrinkSizeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductionCapabilities_IngredientId",
                table: "StoreProductionCapabilities",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductionCapabilities_PreparedItemId",
                table: "StoreProductionCapabilities",
                column: "PreparedItemId");

            migrationBuilder.CreateIndex(
                name: "UX_StoreProductionCapabilities_Store_Ingredient",
                table: "StoreProductionCapabilities",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_StoreProductionCapabilities_Store_PreparedItem",
                table: "StoreProductionCapabilities",
                columns: new[] { "StoreId", "PreparedItemId" },
                unique: true,
                filter: "[PreparedItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ProvinceId",
                table: "Stores",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_WardId",
                table: "Stores",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreStaffingRequirements_CreatedByStaffId",
                table: "StoreStaffingRequirements",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreStaffingRequirements_RequiredRoleId",
                table: "StoreStaffingRequirements",
                column: "RequiredRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreStaffingRequirements_ShiftId",
                table: "StoreStaffingRequirements",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreStaffingRequirements_StoreId_ShiftId_DayOfWeek_EffectiveFrom",
                table: "StoreStaffingRequirements",
                columns: new[] { "StoreId", "ShiftId", "DayOfWeek", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_StoreId",
                table: "StoreToppings",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreToppings_ToppingId",
                table: "StoreToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_Email",
                table: "SupplierContacts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId",
                table: "SupplierContacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId_PhoneNumber",
                table: "SupplierContacts",
                columns: new[] { "SupplierId", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDuplicateWarnings_CreatedSupplierId",
                table: "SupplierDuplicateWarnings",
                column: "CreatedSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDuplicateWarnings_PublicId",
                table: "SupplierDuplicateWarnings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDuplicateWarnings_RequestedByStaffId_Status_ExpiresAtUtc",
                table: "SupplierDuplicateWarnings",
                columns: new[] { "RequestedByStaffId", "Status", "ExpiresAtUtc" });

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
                name: "IX_SupplierReceiptIssues_BranchReceiptId_Status",
                table: "SupplierReceiptIssues",
                columns: new[] { "BranchReceiptId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_BranchReceiptLineId",
                table: "SupplierReceiptIssues",
                column: "BranchReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_DismissedByStaffId",
                table: "SupplierReceiptIssues",
                column: "DismissedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_PurchaseOrderId",
                table: "SupplierReceiptIssues",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_PurchaseOrderLineId",
                table: "SupplierReceiptIssues",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_ReportedByStaffId",
                table: "SupplierReceiptIssues",
                column: "ReportedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_ResolvedByStaffId",
                table: "SupplierReceiptIssues",
                column: "ResolvedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_StoreId_SupplierId_ReportedAtUtc",
                table: "SupplierReceiptIssues",
                columns: new[] { "StoreId", "SupplierId", "ReportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssues_SupplierId",
                table: "SupplierReceiptIssues",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssueTransitions_ActorStaffId",
                table: "SupplierReceiptIssueTransitions",
                column: "ActorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReceiptIssueTransitions_SupplierReceiptIssueId_OccurredAtUtc",
                table: "SupplierReceiptIssueTransitions",
                columns: new[] { "SupplierReceiptIssueId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Active",
                table: "Suppliers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Suppliers_TaxCode",
                table: "Suppliers",
                column: "TaxCode",
                unique: true,
                filter: "[TaxCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStores_StoreId_Active",
                table: "SupplierStores",
                columns: new[] { "StoreId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStores_SupplierId_StoreId",
                table: "SupplierStores",
                columns: new[] { "SupplierId", "StoreId" },
                unique: true);

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
                name: "IX_Wards_Code",
                table: "Wards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wards_ProvinceId",
                table: "Wards",
                column: "ProvinceId");

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
                name: "IX_WorkShiftOpenApprovalRequests_DecidedByStaffId",
                table: "WorkShiftOpenApprovalRequests",
                column: "DecidedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_PublicId",
                table: "WorkShiftOpenApprovalRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_RequestedByStaffId_Status",
                table: "WorkShiftOpenApprovalRequests",
                columns: new[] { "RequestedByStaffId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_RequestKey",
                table: "WorkShiftOpenApprovalRequests",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_SourceStaffShiftId",
                table: "WorkShiftOpenApprovalRequests",
                column: "SourceStaffShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_StoreId_Status_RequestedAtUtc",
                table: "WorkShiftOpenApprovalRequests",
                columns: new[] { "StoreId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkShiftOpenApprovalRequests_TerminalId",
                table: "WorkShiftOpenApprovalRequests",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "UX_WorkShiftOpenApprovals_ActiveContext",
                table: "WorkShiftOpenApprovalRequests",
                columns: new[] { "StoreId", "RequestedByStaffId", "TerminalId" },
                unique: true,
                filter: "[Status] = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_ApprovedByStaffId",
                table: "WorkShifts",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_ClosedByStaffId",
                table: "WorkShifts",
                column: "ClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_CurrentOperatorStaffId",
                table: "WorkShifts",
                column: "CurrentOperatorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_ExceptionClosedByStaffId",
                table: "WorkShifts",
                column: "ExceptionClosedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_OpenContext_Status_AutoCloseAtUtc",
                table: "WorkShifts",
                columns: new[] { "OpenContext", "Status", "AutoCloseAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_SourceStaffShiftId",
                table: "WorkShifts",
                column: "SourceStaffShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_StoreId",
                table: "WorkShifts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_StoreId_RequiresReconciliation",
                table: "WorkShifts",
                columns: new[] { "StoreId", "RequiresReconciliation" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkShifts_ActiveStaff",
                table: "WorkShifts",
                column: "UserId",
                unique: true,
                filter: "[Status] IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')");

            migrationBuilder.CreateIndex(
                name: "UX_WorkShifts_ActiveTerminal",
                table: "WorkShifts",
                column: "PosTerminalId",
                unique: true,
                filter: "[PosTerminalId] IS NOT NULL AND [Status] IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')");

            migrationBuilder.AddForeignKey(
                name: "FK_BranchReceiptLines_InventoryTransactions_InventoryTransactionId",
                table: "BranchReceiptLines",
                column: "InventoryTransactionId",
                principalTable: "InventoryTransactions",
                principalColumn: "InventoryTransactionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchReceiptLines_InventoryTransferCostAllocations_SourceTransferCostAllocationId",
                table: "BranchReceiptLines",
                column: "SourceTransferCostAllocationId",
                principalTable: "InventoryTransferCostAllocations",
                principalColumn: "InventoryTransferCostAllocationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchReceiptLines_PurchaseOrderLines_PurchaseOrderLineId",
                table: "BranchReceiptLines",
                column: "PurchaseOrderLineId",
                principalTable: "PurchaseOrderLines",
                principalColumn: "PurchaseOrderLineId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostAllocations_InventoryCostLayers_InventoryCostLayerId",
                table: "InventoryCostAllocations",
                column: "InventoryCostLayerId",
                principalTable: "InventoryCostLayers",
                principalColumn: "InventoryCostLayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostGapSettlements_InventoryCostLayers_InboundInventoryCostLayerId",
                table: "InventoryCostGapSettlements",
                column: "InboundInventoryCostLayerId",
                principalTable: "InventoryCostLayers",
                principalColumn: "InventoryCostLayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_InventoryTransferCostAllocations_SourceTransferCostAllocationId",
                table: "InventoryCostLayers",
                column: "SourceTransferCostAllocationId",
                principalTable: "InventoryTransferCostAllocations",
                principalColumn: "InventoryTransferCostAllocationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_InventoryTransferDiscrepancyPostings_SourceTransferDiscrepancyPostingId",
                table: "InventoryCostLayers",
                column: "SourceTransferDiscrepancyPostingId",
                principalTable: "InventoryTransferDiscrepancyPostings",
                principalColumn: "InventoryTransferDiscrepancyPostingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceFulfillmentPostings_PurchaseAdviceLines_PurchaseAdviceLineId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "PurchaseAdviceLineId",
                principalTable: "PurchaseAdviceLines",
                principalColumn: "PurchaseAdviceLineId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceFulfillmentPostings_PurchaseOrderLineAllocations_PurchaseOrderLineAllocationId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "PurchaseOrderLineAllocationId",
                principalTable: "PurchaseOrderLineAllocations",
                principalColumn: "PurchaseOrderLineAllocationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceFulfillmentPostings_PurchaseOrderLines_PurchaseOrderLineId",
                table: "PurchaseAdviceFulfillmentPostings",
                column: "PurchaseOrderLineId",
                principalTable: "PurchaseOrderLines",
                principalColumn: "PurchaseOrderLineId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseAdviceLines_RestockSourcingAllocations_RestockSourcingAllocationId",
                table: "PurchaseAdviceLines",
                column: "RestockSourcingAllocationId",
                principalTable: "RestockSourcingAllocations",
                principalColumn: "RestockSourcingAllocationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Accounts_AccountId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_Accounts_AccountId",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Accounts_QuantitySemanticsReviewedByAccountId",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_BranchReceipts_BranchReceiptId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_IngredientSuppliers_IngredientSupplierId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_IngredientSuppliers_IngredientSupplierId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_Ingredients_IngredientId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCostLayers_Ingredients_IngredientId",
                table: "InventoryCostLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocumentDetails_Ingredients_IngredientId",
                table: "InventoryDocumentDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferDetails_Ingredients_IngredientId",
                table: "InventoryTransferDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_Ingredients_IngredientId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Ingredients_IngredientId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockRequests_Ingredients_IngredientId",
                table: "RestockRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Ingredients_IngredientId",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Ingredients_IngredientId",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_InventoryTransactions_InventoryTransactionId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchReceiptLines_InventoryTransferCostAllocations_SourceTransferCostAllocationId",
                table: "BranchReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCostLayers_InventoryTransferCostAllocations_SourceTransferCostAllocationId",
                table: "InventoryCostLayers");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransferDiscrepancyPostings_InventoryTransferCostAllocations_InventoryTransferCostAllocationId",
                table: "InventoryTransferDiscrepancyPostings");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_PreparedItems_PreparedItemId",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockRequests_PreparedItems_PreparedItemId",
                table: "RestockRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_PreparedItems_PreparedItemId",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_PurchaseOrderLines_PurchaseOrderLineId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Recipes_RecipeId",
                table: "ProductionRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockRequests_Recipes_RecipeId",
                table: "RestockRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Recipes_RecipeId",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_RestockRequests_RestockRequestId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_RestockRequests_RestockRequestId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Units_OutputBaseUnitId",
                table: "ProductionRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_Units_BaseUnitId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdviceLines_Units_ProcurementUnitId",
                table: "PurchaseAdviceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_Units_ProcurementUnitId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_InventoryTransfers_InventoryTransferId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Staffs_CompletedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Staffs_CreatedByStaffId",
                table: "ProductionRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdvices_Staffs_CancelledByStaffId",
                table: "PurchaseAdvices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdvices_Staffs_RejectedByStaffId",
                table: "PurchaseAdvices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdvices_Staffs_RequestedByStaffId",
                table: "PurchaseAdvices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdvices_Staffs_ReviewedByStaffId",
                table: "PurchaseAdvices");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_Staffs_CreatedByStaffId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_Staffs_ReleasedByStaffId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRuns_Stores_StoreId",
                table: "ProductionRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseAdvices_Stores_StoreId",
                table: "PurchaseAdvices");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_ProductionRuns_ProductionRunId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_RestockSourcingAllocations_PurchaseAdviceLines_PurchaseAdviceLineId",
                table: "RestockSourcingAllocations");

            migrationBuilder.DropTable(
                name: "AccountPermissionOverrides");

            migrationBuilder.DropTable(
                name: "AccountRoles");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerBanks");

            migrationBuilder.DropTable(
                name: "CustomerPhones");

            migrationBuilder.DropTable(
                name: "CustomerVouchers");

            migrationBuilder.DropTable(
                name: "DocumentNumberCounters");

            migrationBuilder.DropTable(
                name: "DrinkDefaultToppings");

            migrationBuilder.DropTable(
                name: "DrinkImages");

            migrationBuilder.DropTable(
                name: "DrinkSizePriceAudits");

            migrationBuilder.DropTable(
                name: "DrinkSizeToppingPolicyAudits");

            migrationBuilder.DropTable(
                name: "DrinkToppings");

            migrationBuilder.DropTable(
                name: "ForecastPoints");

            migrationBuilder.DropTable(
                name: "IceCarryOvers");

            migrationBuilder.DropTable(
                name: "IceInventoryPostings");

            migrationBuilder.DropTable(
                name: "IceSupplementalIssues");

            migrationBuilder.DropTable(
                name: "ImportAudits");

            migrationBuilder.DropTable(
                name: "ImportItems");

            migrationBuilder.DropTable(
                name: "IngredientSupplierPriceHistories");

            migrationBuilder.DropTable(
                name: "IntelligencePilotRuns");

            migrationBuilder.DropTable(
                name: "InventoryConsolidationLines");

            migrationBuilder.DropTable(
                name: "InventoryCostAllocations");

            migrationBuilder.DropTable(
                name: "InventoryCostGapSettlements");

            migrationBuilder.DropTable(
                name: "InventoryDocumentSnapshotDetails");

            migrationBuilder.DropTable(
                name: "InventoryItemSourceCapabilities");

            migrationBuilder.DropTable(
                name: "InventoryNegativeApprovalLines");

            migrationBuilder.DropTable(
                name: "InventoryWriterModeTransitions");

            migrationBuilder.DropTable(
                name: "InvoiceAuditLogs");

            migrationBuilder.DropTable(
                name: "OperationalAnomalies");

            migrationBuilder.DropTable(
                name: "OperationalShiftScheduleSources");

            migrationBuilder.DropTable(
                name: "OperationalShiftWorkShifts");

            migrationBuilder.DropTable(
                name: "OrderVouchers");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "PosAccessSessions");

            migrationBuilder.DropTable(
                name: "PosCatalogStates");

            migrationBuilder.DropTable(
                name: "PosRecommendationCatalog");

            migrationBuilder.DropTable(
                name: "PosRecommendationExposureItems");

            migrationBuilder.DropTable(
                name: "ProductionCostAllocations");

            migrationBuilder.DropTable(
                name: "ProductionRunInputActuals");

            migrationBuilder.DropTable(
                name: "ProductionRunOutputs");

            migrationBuilder.DropTable(
                name: "ProductionRunTransitions");

            migrationBuilder.DropTable(
                name: "PurchaseAdviceFulfillmentPostings");

            migrationBuilder.DropTable(
                name: "PurchaseAdviceTransitions");

            migrationBuilder.DropTable(
                name: "PurchaseOrderBatchDocumentRevisions");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLineClosures");

            migrationBuilder.DropTable(
                name: "PurchaseOrderReceiptPostings");

            migrationBuilder.DropTable(
                name: "RatingImages");

            migrationBuilder.DropTable(
                name: "RatingReactions");

            migrationBuilder.DropTable(
                name: "RecipeDetails");

            migrationBuilder.DropTable(
                name: "RefundCostGaps");

            migrationBuilder.DropTable(
                name: "RefundCostReversals");

            migrationBuilder.DropTable(
                name: "RequestDeduplications");

            migrationBuilder.DropTable(
                name: "RestockFulfillmentPostings");

            migrationBuilder.DropTable(
                name: "RestockRequestTransitions");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScheduleOptimizationAssignments");

            migrationBuilder.DropTable(
                name: "StaffAddresses");

            migrationBuilder.DropTable(
                name: "StaffAvailabilityExceptions");

            migrationBuilder.DropTable(
                name: "StaffAvailabilityRules");

            migrationBuilder.DropTable(
                name: "StaffNotifications");

            migrationBuilder.DropTable(
                name: "StaffPhones");

            migrationBuilder.DropTable(
                name: "StaffScopes");

            migrationBuilder.DropTable(
                name: "StaffTimeOffs");

            migrationBuilder.DropTable(
                name: "StaffWorkConstraints");

            migrationBuilder.DropTable(
                name: "StockAlertTransitions");

            migrationBuilder.DropTable(
                name: "StockTakeDetails");

            migrationBuilder.DropTable(
                name: "StoreDrinks");

            migrationBuilder.DropTable(
                name: "StoreInventoryWriterConfigurations");

            migrationBuilder.DropTable(
                name: "StoreMenuItemAudits");

            migrationBuilder.DropTable(
                name: "StoreProductionCapabilities");

            migrationBuilder.DropTable(
                name: "StoreStaffingRequirements");

            migrationBuilder.DropTable(
                name: "StoreToppings");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "SupplierDuplicateWarnings");

            migrationBuilder.DropTable(
                name: "SupplierPhones");

            migrationBuilder.DropTable(
                name: "SupplierReceiptIssueTransitions");

            migrationBuilder.DropTable(
                name: "SupplierStores");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TransactionLogs");

            migrationBuilder.DropTable(
                name: "UnitConversions");

            migrationBuilder.DropTable(
                name: "VoucherUsages");

            migrationBuilder.DropTable(
                name: "WheelSpins");

            migrationBuilder.DropTable(
                name: "WorkShiftOpenApprovalRequests");

            migrationBuilder.DropTable(
                name: "DrinkSizeToppingPolicies");

            migrationBuilder.DropTable(
                name: "IceAllocations");

            migrationBuilder.DropTable(
                name: "ImportGroups");

            migrationBuilder.DropTable(
                name: "InventoryNegativeCostGaps");

            migrationBuilder.DropTable(
                name: "InventoryDocumentSnapshots");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PointTransactionTypes");

            migrationBuilder.DropTable(
                name: "PosRecommendationExposures");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLineAllocations");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "SalesCostAllocations");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "ScheduleOptimizationProposals");

            migrationBuilder.DropTable(
                name: "OtpChallenges");

            migrationBuilder.DropTable(
                name: "ScopeTypes");

            migrationBuilder.DropTable(
                name: "StockTakeSessions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SupplierReceiptIssues");

            migrationBuilder.DropTable(
                name: "WheelPrizes");

            migrationBuilder.DropTable(
                name: "IcePolicies");

            migrationBuilder.DropTable(
                name: "OperationalShifts");

            migrationBuilder.DropTable(
                name: "ImportSourceDocuments");

            migrationBuilder.DropTable(
                name: "InventoryNegativeApprovals");

            migrationBuilder.DropTable(
                name: "SalesCostGaps");

            migrationBuilder.DropTable(
                name: "PurchaseOrderBatchLines");

            migrationBuilder.DropTable(
                name: "PermissionGroups");

            migrationBuilder.DropTable(
                name: "ForecastRuns");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "WheelConfigs");

            migrationBuilder.DropTable(
                name: "ImportSessions");

            migrationBuilder.DropTable(
                name: "OrderToppings");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "StoreMenuItems");

            migrationBuilder.DropTable(
                name: "DrinkSizes");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "BranchReceipts");

            migrationBuilder.DropTable(
                name: "IngredientSuppliers");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "InventoryConsolidationRuns");

            migrationBuilder.DropTable(
                name: "StoreInventories");

            migrationBuilder.DropTable(
                name: "InventoryTransferCostAllocations");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");

            migrationBuilder.DropTable(
                name: "BranchReceiptLines");

            migrationBuilder.DropTable(
                name: "InventoryDocumentDetails");

            migrationBuilder.DropTable(
                name: "InventoryTransferDiscrepancyPostings");

            migrationBuilder.DropTable(
                name: "OrderRefunds");

            migrationBuilder.DropTable(
                name: "InventoryDocuments");

            migrationBuilder.DropTable(
                name: "InventoryTransferDetails");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "RestockRequestFulfillments");

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
                name: "MemberLevels");

            migrationBuilder.DropTable(
                name: "PosTerminals");

            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "StaffShiftStatuses");

            migrationBuilder.DropTable(
                name: "PreparedItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "PurchaseOrderBatches");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Drinks");

            migrationBuilder.DropTable(
                name: "Sizes");

            migrationBuilder.DropTable(
                name: "Toppings");

            migrationBuilder.DropTable(
                name: "DrinkCategories");

            migrationBuilder.DropTable(
                name: "ProductTypes");

            migrationBuilder.DropTable(
                name: "RestockRequests");

            migrationBuilder.DropTable(
                name: "StockAlerts");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "InventoryTransfers");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "Wards");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "ProductionRuns");

            migrationBuilder.DropTable(
                name: "PurchaseAdviceLines");

            migrationBuilder.DropTable(
                name: "PurchaseAdvices");

            migrationBuilder.DropTable(
                name: "RestockSourcingAllocations");
        }
    }
}
