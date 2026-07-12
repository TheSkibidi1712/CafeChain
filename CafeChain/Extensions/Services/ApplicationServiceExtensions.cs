// ==========================================
// Application - Interfaces
// ==========================================
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Settings;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Interfaces.AI;

// ==========================================
// Application - Services
// ==========================================
using CafeChain.Application.Services;
using CafeChain.Application.Services.Accounts;
using CafeChain.Application.Services.Admin;
using CafeChain.Application.Services.Admin.Categories;
using CafeChain.Application.Services.Admin.Dashboard;
using CafeChain.Application.Services.Admin.Drinks;
using CafeChain.Application.Services.Admin.DrinkSizes;
using CafeChain.Application.Services.Admin.DrinkToppings;
using CafeChain.Application.Services.Admin.Ingredients;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Application.Services.Admin.InventoryTransfers;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Admin.Settings;
using CafeChain.Application.Services.Admin.Sizes;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Admin.Toppings;
using CafeChain.Application.Services.Admin.Vouchers;
using CafeChain.Application.Services.Attendance;
using CafeChain.Application.Services.Cart;
using CafeChain.Application.Services.Cloudinaries;
using CafeChain.Application.Services.Customers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Inventory;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Application.Services.Security;
using CafeChain.Application.Services.Workers;
using CafeChain.Application.Workers;
using CafeChain.Application.Services.Admin.Permissions;
using CafeChain.Application.Services.Systems;
using CafeChain.Application.Services.AI;

namespace CafeChain.Extensions.Services
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddCafeChainApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUserContext, UserContext>();
            services.AddScoped<IAIService, AIService>();

            // Account
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<IEmailService, EmailService>();

            // Customer
            services.AddScoped<ICustomerService, CustomerService>();

            // Home / POS / Cart
            services.AddScoped<IDrinkService, DrinkService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IOrderService, OrderService>();

            // Inventory Core
            services.AddScoped<IInventoryService, InventoryService>();
            // EstimatedBomCost (package-normalized COMPLETE/INCOMPLETE) — Issue #117
            services.AddScoped<IEstimatedBomCostService, EstimatedBomCostService>();
            // Purchase/unit read-only audit — Issue #113 Checkpoint A
            services.AddScoped<IPurchaseUnitAuditService, PurchaseUnitAuditService>();
            // PreparedItem inventory dual-read and dry-run analysis — Issue #115
            services.AddScoped<IInventoryItemIdentityResolver, InventoryItemIdentityResolver>();
            services.AddScoped<IPreparedItemInventoryCompatibilityAnalyzer, PreparedItemInventoryCompatibilityAnalyzer>();
            services.AddScoped<IInventoryWriterModeService, InventoryWriterModeService>();
            services.AddScoped<IStoreInventoryWriteResolver, StoreInventoryWriteResolver>();
            services.AddScoped<IInventoryDeductionService, InventoryDeductionService>();
            services.AddScoped<INegativeInventoryService, NegativeInventoryService>();

            // System
            services.AddScoped<IRequestDeduplicationService, RequestDeduplicationService>();

            // File
            services.AddScoped<IFileService, FileService>();

            // Geocoding
            services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();

            // Admin - Category
            services.AddScoped<IAdminCategoryService, AdminCategoryService>();

            // Admin - Sizes
            services.AddScoped<IAdminSizeService, AdminSizeService>();

            // Admin - DrinkSizes
            services.AddScoped<IAdminDrinkSizeService, AdminDrinkSizeService>();

            // Admin - Toppings
            services.AddScoped<IAdminToppingService, AdminToppingService>();

            // Admin - DrinkToppings
            services.AddScoped<IAdminDrinkToppingService, AdminDrinkToppingService>();

            // Admin - Drinks
            services.AddScoped<IAdminDrinkService, AdminDrinkService>();

            // Admin - Voucher
            services.AddScoped<IAdminVoucherService, AdminVoucherService>();
            services.AddScoped<IAdminWheelService, AdminWheelService>();

            // Admin - Staff
            services.AddScoped<IAdminStaffService, AdminStaffService>();
            services.AddScoped<IAdminStaffShiftService, AdminStaffShiftService>();

            // Admin - Ingredients
            services.AddScoped<IAdminIngredientService, AdminIngredientService>();

            // Admin - PreparedItem master (BTP stock identity) — Issue #116
            services.AddScoped<CafeChain.Application.Interfaces.Admin.PreparedItems.IAdminPreparedItemService,
                CafeChain.Application.Services.Admin.PreparedItems.AdminPreparedItemService>();

            // Admin - Recipes (+ #112 BTP output normalizer)
            services.AddScoped<IRecipeOutputNormalizer, RecipeOutputNormalizer>();
            services.AddScoped<IAdminRecipeService, AdminRecipeService>();

            // Admin - Production runs (#119 intent; #120 stock apply)
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunService,
                CafeChain.Application.Services.Admin.Production.ProductionRunService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunExecutionService,
                CafeChain.Application.Services.Admin.Production.ProductionRunExecutionService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.IInventoryWriterCapabilityProvider,
                CafeChain.Application.Services.Admin.Production.ProductionPreparedWriterCapabilityProvider>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.IInventoryWriterCapabilityProvider,
                CafeChain.Application.Services.Inventories.PosPreparedWriterCapabilityProvider>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.IInventoryWriterCapabilityProvider,
                CafeChain.Application.Services.Inventories.AlertRestockPreparedIdentityCapabilityProvider>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.IInventoryWriterCapabilityProvider,
                CafeChain.Application.Services.Inventories.ConsolidationOrNoopEvidenceCapabilityProvider>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.ILegacyBtpConsolidationService,
                CafeChain.Application.Services.Inventories.LegacyBtpConsolidationService>();

            // Issue #124 — cutover reconciliation, schema probe, global legacy kill switch
            // Bind from configuration/env: InventoryWriter__LegacyBtpWritesDisabled (do not commit secrets/appsettings).
            services.AddOptions<CafeChain.Application.Options.InventoryWriterGlobalOptions>()
                .BindConfiguration(CafeChain.Application.Options.InventoryWriterGlobalOptions.SectionName);
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.IInventorySchemaReadinessProbe,
                CafeChain.Application.Services.Inventories.InventorySchemaReadinessProbe>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Inventories.ICutoverReconciliationService,
                CafeChain.Application.Services.Inventories.CutoverReconciliationService>();

            // Admin - Inventory Documents
            services.AddScoped<IAdminInventoryDocumentService, AdminInventoryDocumentService>();
            services.AddScoped<IAdminInventoryDocumentExportService, AdminInventoryDocumentExportService>();
            services.AddScoped<IAdminInventoryDocumentValidationService, AdminInventoryDocumentValidationService>();
            services.AddScoped<IAdminInventoryDocumentSnapshotService, AdminInventoryDocumentSnapshotService>();
            services.AddScoped<IAdminInventoryDocumentProcessService, AdminInventoryDocumentProcessService>();
            services.AddScoped<IAdminInventoryDocumentConfirmService, AdminInventoryDocumentConfirmService>();
            services.AddScoped<IAdminInventoryDocumentCreateService, AdminInventoryDocumentCreateService>();

            // Admin - Inventory Transfers
            services.AddScoped<IAdminInventoryTransferService, AdminInventoryTransferService>();

            // Admin - Store Inventories
            services.AddScoped<IAdminStoreInventoryService, AdminStoreInventoryService>();
            // Issue #104 — MinStockLevel thresholds (Admin)
            services.AddScoped<IInventoryThresholdService, InventoryThresholdService>();

            // Admin - Suppliers
            services.AddScoped<IAdminSupplierService, AdminSupplierService>();

            // Admin - Orders Dashboard
            services.AddScoped<IAdminOrderService, AdminOrderService>();

            // Security
            services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();
            services.AddScoped<IAttendanceSecurityService, AttendanceSecurityService>();
            services.AddScoped<IAttendanceActionService, AttendanceActionService>();

            // Admin - Dashboard
            services.AddScoped<IDashboardService, DashboardService>();

            // Admin - Settings
            services.AddScoped<IAdminSettingService, AdminSettingService>();

            // HR & Attendance
            services.AddScoped<IHrAttendanceService, HrAttendanceService>();
            services.AddScoped<IWorkShiftService, WorkShiftService>();
            services.AddScoped<ISupervisorAuthService, SupervisorAuthService>();

            // POS
            services.AddScoped<IPOSOrderService, POSOrderService>();
            services.AddScoped<IPosBranchInventoryService, PosBranchInventoryService>();
            services.AddScoped<IPrintDispatcher, PrintDispatcher>();
            services.AddScoped<IEscPosBuilder, EscPosReceiptBuilder>();
            services.AddScoped<IPayOSWebhookProcessor, PayOSWebhookProcessor>();
            services.AddScoped<IOtpApprovalService, OtpApprovalService>();

            // Shared unit conversion (POS catalog + inventory deduction + COGS)
            // Physical (Unit-domain kg↔g, l↔ml) then ingredient-specific — Issue #110
            services.AddScoped<IPhysicalUnitConversionService, PhysicalUnitConversionService>();
            services.AddScoped<IUnitConversionService, UnitConversionService>();
            // Ingredient supplier package definition validation — Issue #111
            services.AddScoped<IIngredientSupplierPackageValidator, IngredientSupplierPackageValidator>();

            // Inventory stock alerts (Issue #97) + shortage report (Issue #98) + manager confirm (Issue #99) + restock (Issue #100)
            services.AddScoped<IStockAlertService, StockAlertService>();
            services.AddScoped<IStockShortageReportService, StockShortageReportService>();
            services.AddScoped<IStockAlertManagerService, StockAlertManagerService>();
            services.AddScoped<IRestockRequestService, RestockRequestService>();

            // Staff notifications read/mark (Issue #101)
            services.AddScoped<CafeChain.Application.Interfaces.Operations.IStaffNotificationQueryService,
                CafeChain.Application.Services.Operations.StaffNotificationQueryService>();

            // Permissions
            services.AddScoped<IAdminPermissionService, AdminPermissionService>();

            return services;
        }
    }
}
