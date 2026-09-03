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
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.AIImport;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.AppLauncher;
using CafeChain.Application.Interfaces.StaffHub;

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
using CafeChain.Application.Services.Cart;
using CafeChain.Application.Services.Cloudinaries;
using CafeChain.Application.Services.StaffHub;
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
using CafeChain.Application.Services.AIImport;
using CafeChain.Application.Services.Admin.StoreScope;
using CafeChain.Application.Services.AppLauncher;
using CafeChain.Application.Options;
using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Extensions.Services
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddCafeChainApplicationServices(this IServiceCollection services)
        {
            // Keeps minimal CLI hosts self-contained. The web host configures the
            // persistent production key repository before registering this module.
            services.AddDataProtection().SetApplicationName("CafeChain");
            services.AddSingleton(TimeProvider.System);
            services.AddOptions<WorkShiftOptions>()
                .BindConfiguration(WorkShiftOptions.SectionName);
            services.AddOptions<AIImportOptions>()
                .BindConfiguration(AIImportOptions.SectionName)
                .Validate(x => x.MaxFileBytes > 0 && x.MaxFilesPerSession > 0
                               && x.MaxTotalUploadBytesPerSession >= x.MaxFileBytes
                               && x.MaxTotalCandidatesPerSession > 0 && x.MaxTotalExtractedCharactersPerSession > 0
                               && x.MaxTotalAIChunksPerSession > 0 && x.MaxTotalOcrPagesPerSession > 0
                               && x.MaxExpandedBytes > 0 && x.MaxCompressionRatio > 0
                               && x.MaxSheets > 0 && x.MaxRowsPerSheet > 0 && x.MaxTotalRows > 0
                               && x.MaxColumnsPerSheet > 0 && x.MaxTotalCells > 0 && x.MaxRegionsPerSheet > 0
                               && x.MaxAiSampleRows > 0 && x.SessionLifetimeHours > 0
                               && x.DefaultPageSize > 0 && x.MaximumPageSize >= x.DefaultPageSize
                               && x.HighConfidenceThreshold is > 0 and <= 1
                               && x.ReviewConfidenceThreshold is > 0 and <= 1
                               && x.HighConfidenceThreshold >= x.ReviewConfidenceThreshold
                               && x.DocxMaxParagraphs > 0 && x.DocxMaxTables > 0
                               && x.DocxMaxTableRows > 0 && x.DocxMaxCells > 0
                               && x.DocumentMaxExtractedCharacters > 0 && x.PdfMaxPages > 0
                               && x.PdfMaxTextBlocks > 0 && x.PdfMaxImages > 0
                               && x.PdfOcrImageAreaRatioThreshold is > 0 and <= 1
                               && x.AIChunkMaxCharacters > x.AIChunkOverlapCharacters
                               && x.AIChunkOverlapCharacters >= 0 && x.MaxAIChunks > 0
                               && x.OcrMaxPages > 0 && x.OcrMaxPages <= x.PdfMaxPages
                               && x.OcrMaxRenderedPixelsPerPage > 0 && x.OcrMaxTotalRenderedPixels >= x.OcrMaxRenderedPixelsPerPage
                               && x.OcrRenderDpi is >= 72 and <= 600
                               && x.OcrPageTimeoutSeconds > 0 && x.OcrTotalTimeoutSeconds >= x.OcrPageTimeoutSeconds
                                && x.OcrMaxConcurrentPages is > 0 and <= 16
                                && x.OcrReviewConfidenceThreshold is > 0 and <= 1
                                && string.Equals(x.OcrProvider, "TesseractLocal", StringComparison.Ordinal)
                                && !string.IsNullOrWhiteSpace(x.OcrExecutablePath)
                                && !string.IsNullOrWhiteSpace(x.OcrTessdataPath)
                                && !string.IsNullOrWhiteSpace(x.OcrLanguages),
                    "AIImport options không hợp lệ.")
                .ValidateOnStart();
            services.AddScoped<IUserContext, UserContext>();
            services.AddScoped<IAIService, AIService>();
            services.AddScoped<IAIImagePipelineService, AIImagePipelineService>();
            services.AddSingleton<IAISkillCatalog, AISkillCatalog>();
            services.AddSingleton<IAISuggestionHistoryStore, AISuggestionHistoryStore>();
            services.AddSingleton<IVisualSpecificationBuilder, VisualSpecificationBuilder>();
            services.AddSingleton<IPexelsMetadataScorer, PexelsMetadataScorer>();

            // Account
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IAppLauncherService, AppLauncherService>();
            services.AddSingleton<IPrintBridgePresenceTracker, PrintBridgePresenceTracker>();
            services.AddSingleton<IPosLaunchCoordinator, PosLaunchCoordinator>();
            services.AddHttpClient();
            services.AddOptions<PosLauncherOptions>()
                .BindConfiguration(PosLauncherOptions.SectionName);
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
            services.AddScoped<IOperationalIceService, OperationalIceService>();
            services.AddScoped<IOperationalIceReservationConsumptionService, OperationalIceReservationConsumptionService>();
            services.AddScoped<IOperationalIceReportService, OperationalIceReportService>();
            services.AddScoped<IOperationalIceReportPdfRenderer, OperationalIceReportPdfRenderer>();
            services.AddScoped<IInventoryIssueSettingsProvider, InventoryIssueSettingsProvider>();
            services.AddScoped<IInventoryIssuePolicy, InventoryIssuePolicy>();
            // Issue #132 — shared FIFO cost-layer consumption (production; POS later)
            services.AddScoped<IInventoryCostLayerConsumptionService, InventoryCostLayerConsumptionService>();
            services.AddScoped<IPurchaseOrderBatchDocumentService, PurchaseOrderBatchDocumentService>();
            services.AddScoped<IPurchaseOrderBatchPdfRenderer, PurchaseOrderBatchPdfRenderer>();
            services.AddSingleton<IPurchaseOrderBatchDocumentStorage, PurchaseOrderBatchDocumentStorage>();

            // System
            services.AddScoped<IRequestDeduplicationService, RequestDeduplicationService>();
            services.AddScoped<IAIImportExcelParser, AIImportExcelParser>();
            services.AddSingleton<IAIImportSchemaRegistry, AIImportSchemaRegistry>();
            services.AddScoped<IAIImportRegionAnalyzer, AIImportRegionAnalyzer>();
            services.AddScoped<IAIImportSourceParser, AIImportExcelSourceParser>();
            services.AddScoped<IAIImportSourceParser, AIImportDocxSourceParser>();
            services.AddScoped<IAIImportSourceParser, AIImportPdfSourceParser>();
            services.AddScoped<IAIImportDocumentAiExtractor, AIImportDocumentAiExtractor>();
            services.AddSingleton<IAIImportPdfPageRenderer, PdfiumAIImportPdfPageRenderer>();
            services.AddSingleton<ITesseractProcessRunner, TesseractProcessRunner>();
            services.AddScoped<IAIImportOcrProvider, TesseractLocalOcrProvider>();
            services.AddScoped<IAIImportOcrRuntimeSettings, AIImportOcrRuntimeSettings>();
            services.AddScoped<IAIImportDocumentPipeline, AIImportDocumentPipeline>();
            services.AddScoped<AIImportCandidateValidator>();
            services.AddSingleton<AIImportResolutionEngine>();
            services.AddScoped<AIImportPreviewValidator>();
            services.AddSingleton<AIImportAnalysisCoordinator>();
            services.AddSingleton<AIImportPreviewMutationCoordinator>();
            services.AddSingleton<AIImportConfirmCoordinator>();
            services.AddSingleton<AIImportSessionQuery>();
            services.AddScoped<AIImportEntityCreator>();
            services.AddSingleton<AIImportEntityRegistry>();
            services.AddScoped<IAIImportService, AIImportService>();

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
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profitability.IDrinkSizeRecipeResolver,
                CafeChain.Application.Services.Admin.Profitability.DrinkSizeRecipeResolver>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profitability.IDrinkSizeToppingPolicyService,
                CafeChain.Application.Services.Admin.Profitability.DrinkSizeToppingPolicyService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profitability.IDrinkSizeProfitabilityQueryService,
                CafeChain.Application.Services.Admin.Profitability.DrinkSizeProfitabilityQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profitability.IPriceSuggestionService,
                CafeChain.Application.Services.Admin.Profitability.PriceSuggestionService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profitability.IDrinkSizePricingService,
                CafeChain.Application.Services.Admin.Profitability.DrinkSizePricingService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreMenuBackfillPlanner,
                CafeChain.Application.Services.Admin.StoreMenu.StoreMenuBackfillPlanner>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreMenuProvisioningService,
                CafeChain.Application.Services.Admin.StoreMenu.StoreMenuProvisioningService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreMenuAvailabilityEvaluator,
                CafeChain.Application.Services.Admin.StoreMenu.StoreMenuAvailabilityEvaluator>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreCatalogVersionService,
                CafeChain.Application.Services.Admin.StoreMenu.StoreCatalogVersionService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreMenuPricingService,
                CafeChain.Application.Services.Admin.StoreMenu.StoreMenuPricingService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.StoreMenu.IStoreMenuWorkspaceService,
                CafeChain.Application.Services.Admin.StoreMenu.StoreMenuWorkspaceService>();
            services.AddScoped<CafeChain.Application.Interfaces.POS.IPOSCatalogSnapshotService,
                CafeChain.Application.Services.POS.POSCatalogSnapshotService>();
            services.AddScoped<CafeChain.Application.Interfaces.POS.IPOSIceCustomizationService,
                CafeChain.Application.Services.POS.POSIceCustomizationService>();
            services.AddScoped<CafeChain.Application.Interfaces.POS.IPOSStoreMenuSaleValidator,
                CafeChain.Application.Services.POS.POSStoreMenuSaleValidator>();

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
            services.AddScoped<IShiftOptimizationService, ShiftOptimizationService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Profiles.IAdminProfileService,
                CafeChain.Application.Services.Admin.Profiles.AdminProfileService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Stores.IAdminStoreService,
                CafeChain.Application.Services.Admin.Stores.AdminStoreService>();

            // Admin - Ingredients
            services.AddScoped<IAdminIngredientService, AdminIngredientService>();

            // Admin - PreparedItem master (BTP stock identity) — Issue #116
            services.AddScoped<CafeChain.Application.Interfaces.Admin.PreparedItems.IAdminPreparedItemService,
                CafeChain.Application.Services.Admin.PreparedItems.AdminPreparedItemService>();

            // Admin - Recipes (+ #112 BTP output normalizer)
            services.AddScoped<IRecipeOutputNormalizer, RecipeOutputNormalizer>();
            services.AddScoped<ICurrentRecipeResolver, CurrentRecipeResolver>();
            services.AddScoped<IAdminRecipeService, AdminRecipeService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IAdminRecipeQueryService,
                CafeChain.Application.Services.Admin.Recipes.AdminRecipeQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IRecipeWhereUsedQueryService,
                CafeChain.Application.Services.Admin.Recipes.RecipeWhereUsedQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IRecipeVersionEvidenceQueryService,
                CafeChain.Application.Services.Admin.Recipes.RecipeVersionEvidenceQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IBomDataHealthEvaluator,
                CafeChain.Application.Services.Admin.Recipes.BomDataHealthEvaluator>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IRecipeBomTreeQueryService,
                CafeChain.Application.Services.Admin.Recipes.RecipeBomTreeQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Admin.Actor.IAdminActorContextAccessor,
                CafeChain.Application.Services.Admin.Actor.AdminActorContextAccessor>();
            services.AddScoped<IAdminSelectedStoreContext, AdminSessionSelectedStoreContext>();
            services.AddScoped<IAdminStoreScopeResolver, AdminStoreScopeResolver>();

            // Admin - Production runs (#119 intent; #120 stock apply)
            services.AddOptions<CafeChain.Application.Options.ProductionOperationsOptions>()
                .BindConfiguration(CafeChain.Application.Options.ProductionOperationsOptions.SectionName)
                .Validate(
                    options => options.DefaultYieldVarianceTolerancePercent > 0m
                        && options.DefaultYieldVarianceTolerancePercent <= 100m,
                    "Ngưỡng chênh lệch sản lượng mặc định phải lớn hơn 0 và không vượt quá 100%.")
                .ValidateOnStart();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunService,
                CafeChain.Application.Services.Admin.Production.ProductionRunService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionReadinessService,
                CafeChain.Application.Services.Admin.Production.ProductionReadinessService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionSourceEligibilityService,
                CafeChain.Application.Services.Admin.Production.ProductionSourceEligibilityService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IPreparedItemProductionCapabilityService,
                CafeChain.Application.Services.Admin.Production.PreparedItemProductionCapabilityService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunOperationsService,
                CafeChain.Application.Services.Admin.Production.ProductionRunOperationsService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunAcceptanceService,
                CafeChain.Application.Services.Admin.Production.ProductionRunAcceptanceService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionRunQueryService,
                CafeChain.Application.Services.Admin.Production.ProductionRunQueryService>();
            services.AddScoped<
                CafeChain.Application.Interfaces.Admin.Production.IProductionLegacyAuditService,
                CafeChain.Application.Services.Admin.Production.ProductionLegacyAuditService>();
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
            services.AddScoped<IPreparedItemInventoryBootstrapService, PreparedItemInventoryBootstrapService>();
            services.AddScoped<IPreparedItemReplenishmentReadService, PreparedItemReplenishmentReadService>();

            // Admin - Suppliers
            services.AddScoped<IAdminSupplierService, AdminSupplierService>();
            services.AddScoped<ISupplierVisibilityRepairService, SupplierVisibilityRepairService>();
            services.AddScoped<ISupplierProcurementDataQualityService, SupplierProcurementDataQualityService>();

            // Admin - Orders Dashboard
            services.AddScoped<IAdminOrderService, AdminOrderService>();

            // Security
            services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();
            services.AddScoped<IOrderAccessAuthorizationService, OrderAccessAuthorizationService>();

            // Admin - Dashboard
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IDashboardAuthorizationService, DashboardAuthorizationService>();
            services.AddSingleton<IBusinessDateService, BusinessDateService>();
            services.AddScoped<IDashboardIntelligenceService, DashboardIntelligenceService>();
            services.AddOptions<DashboardIntelligenceOptions>()
                .BindConfiguration(DashboardIntelligenceOptions.SectionName);
            services.AddScoped<CafeChain.Application.Interfaces.AI.IForecastService,
                CafeChain.Application.Services.AI.ForecastService>();
            services.AddScoped<CafeChain.Application.Interfaces.AI.ISupplierIntelligenceService,
                CafeChain.Application.Services.AI.SupplierIntelligenceService>();
            services.AddScoped<CafeChain.Application.Interfaces.AI.ISupplierIntelligenceFeatureGate,
                CafeChain.Application.Services.AI.SupplierIntelligenceFeatureGate>();
            services.AddOptions<ForecastingOptions>().BindConfiguration(ForecastingOptions.SectionName);
            services.AddOptions<SupplierIntelligenceOptions>().BindConfiguration(SupplierIntelligenceOptions.SectionName);
            services.AddScoped<CafeChain.Application.Interfaces.AI.IPosRecommendationService,
                CafeChain.Application.Services.AI.PosRecommendationService>();
            services.AddScoped<CafeChain.Application.Interfaces.AI.IAnomalyDetectionService,
                CafeChain.Application.Services.AI.AnomalyDetectionService>();
            services.AddOptions<PosRecommendationOptions>().BindConfiguration(PosRecommendationOptions.SectionName);
            services.AddOptions<AnomalyDetectionOptions>().BindConfiguration(AnomalyDetectionOptions.SectionName);

            // Admin - Settings
            services.AddScoped<IAdminSettingService, AdminSettingService>();

            services.AddScoped<IWorkShiftService, WorkShiftService>();
            services.AddScoped<IWorkShiftOpenApprovalService, WorkShiftOpenApprovalService>();
            services.AddScoped<IWorkShiftOpenApprovalPublisher,
                CafeChain.Infrastructure.Realtime.SignalRWorkShiftOpenApprovalPublisher>();
            services.AddScoped<IPosSessionExchangeService, PosSessionExchangeService>();
            services.AddScoped<IPosAccessSessionService, PosAccessSessionService>();
            services.AddScoped<IWorkShiftAuditService, WorkShiftAuditService>();
            services.AddScoped<IStaffScheduleService, StaffScheduleService>();

            // POS
            services.AddScoped<IPOSOrderService, POSOrderService>();
            services.AddScoped<IOrderRefundService, OrderRefundService>();
            services.AddScoped<IPOSPaymentCancellationService, POSPaymentCancellationService>();
            services.AddScoped<IPosBranchInventoryService, PosBranchInventoryService>();
            services.AddScoped<IPrintDispatcher, PrintDispatcher>();
            services.AddScoped<IEscPosBuilder, EscPosReceiptBuilder>();
            services.AddScoped<IPayOSWebhookProcessor, PayOSWebhookProcessor>();
            services.AddScoped<IOtpApprovalService, OtpApprovalService>();
            services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
            services.AddSingleton<IOtpProtectedPayloadService, OtpProtectedPayloadService>();
            services.AddSingleton<IOtpPayloadFingerprintService, OtpPayloadFingerprintService>();
            services.AddScoped<IWorkShiftNotificationPublisher,
                CafeChain.Infrastructure.Realtime.SignalRWorkShiftNotificationPublisher>();
            services.AddScoped<CafeChain.Application.Interfaces.StaffHub.IStaffScheduleNotificationPublisher,
                CafeChain.Infrastructure.Realtime.SignalRStaffScheduleNotificationPublisher>();
            services.AddScoped<IPosAccessSessionPublisher,
                CafeChain.Infrastructure.Realtime.SignalRPosAccessSessionPublisher>();

            // Shared unit conversion (POS catalog + inventory deduction + COGS)
            // Physical (Unit-domain kg↔g, l↔ml) then ingredient-specific — Issue #110
            services.AddScoped<IPhysicalUnitConversionService, PhysicalUnitConversionService>();
            services.AddScoped<IUnitConversionService, UnitConversionService>();
            // #127 Admin unit conversion / package semantics UX
            services.AddScoped<CafeChain.Application.Interfaces.Admin.UnitConversions.IAdminUnitConversionService,
                CafeChain.Application.Services.Admin.UnitConversions.AdminUnitConversionService>();
            // Ingredient supplier package definition validation — Issue #111
            services.AddScoped<IIngredientSupplierPackageValidator, IngredientSupplierPackageValidator>();

            // Inventory stock alerts (Issue #97) + shortage report (Issue #98) + manager confirm (Issue #99) + restock (Issue #100)
            services.AddScoped<IStockAlertService, StockAlertService>();
            services.AddScoped<IStockShortageReportService, StockShortageReportService>();
            services.AddScoped<IStockAlertManagerService, StockAlertManagerService>();
            services.AddScoped<IRestockRequestService, RestockRequestService>();
            services.AddScoped<IPurchaseSourceEligibilityService, PurchaseSourceEligibilityService>();
            services.AddScoped<IReorderSuggestionService, ReorderSuggestionService>();
            services.AddScoped<IReorderSuggestionTokenService, ReorderSuggestionTokenService>();
            services.AddScoped<IReorderSuggestionAuthorizationService, ReorderSuggestionAuthorizationService>();
            services.AddScoped<IReorderSuggestionConfirmationService, ReorderSuggestionConfirmationService>();
            services.AddScoped<IInventoryReorderNotificationService, InventoryReorderNotificationService>();
            services.AddOptions<InventoryReorderNotificationOptions>()
                .BindConfiguration(InventoryReorderNotificationOptions.SectionName);
            services.AddScoped<IReorderIncomingQuantityProvider, PurchaseOrderQuantityProvider>();
            // Issue #128 — restock workflow + branch receipt posting
            services.AddScoped<IRestockRequestWorkflowService, RestockRequestWorkflowService>();
            services.AddScoped<IRestockFulfillmentPostingService, RestockFulfillmentPostingService>();
            services.AddScoped<IRestockPurchaseAllocationProvider, PurchaseOrderQuantityProvider>();
            services.AddScoped<IRestockAllocationService, RestockAllocationService>();
            services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
            services.AddScoped<IPurchaseOrderConsistencyService, PurchaseOrderConsistencyService>();
            services.AddScoped<IPurchaseAdviceService, PurchaseAdviceService>();
            services.AddScoped<IPurchaseAdviceFulfillmentService, PurchaseAdviceFulfillmentService>();
            services.AddScoped<IPurchaseAdviceConsolidationService, PurchaseAdviceConsolidationService>();
            services.AddScoped<IPurchaseOrderBatchService, PurchaseOrderBatchService>();
            services.AddScoped<IDuplicatePurchaseOrderRepairService, DuplicatePurchaseOrderRepairService>();
            services.AddScoped<ISupplierQualityService, SupplierQualityService>();
            services.AddScoped<IBranchReceiptService, BranchReceiptService>();

            // Staff notifications read/mark (Issue #101)
            services.AddScoped<CafeChain.Application.Interfaces.Operations.IStaffNotificationQueryService,
                CafeChain.Application.Services.Operations.StaffNotificationQueryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Operations.ITerminalRegistrationNotificationService,
                CafeChain.Application.Services.Operations.TerminalRegistrationNotificationService>();
            services.AddScoped<CafeChain.Application.Interfaces.Operations.IInventoryNotificationAudienceResolver,
                CafeChain.Application.Services.Operations.InventoryNotificationAudienceResolver>();
            services.AddScoped<CafeChain.Application.Interfaces.Operations.IInventoryNotificationDeliveryService,
                CafeChain.Application.Services.Operations.InventoryNotificationDeliveryService>();
            services.AddScoped<CafeChain.Application.Interfaces.Operations.IInventoryNotificationPublisher,
                CafeChain.Infrastructure.Realtime.SignalRInventoryNotificationPublisher>();
            services.AddScoped<CafeChain.Application.Interfaces.POS.IOperationalOtpNotificationPublisher,
                CafeChain.Infrastructure.Realtime.SignalROperationalOtpNotificationPublisher>();
            services.AddOptions<InventoryNotificationOptions>()
                .BindConfiguration(InventoryNotificationOptions.SectionName);

            // Permissions
            services.AddScoped<IAdminPermissionService, AdminPermissionService>();

            return services;
        }
    }
}
