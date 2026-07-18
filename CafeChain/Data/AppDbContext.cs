using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Data.Configurations;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.StockTake;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Locations;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Operations;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using CafeChain.Models.Permissions;
using CafeChain.Models.Inventories.Approvals;
namespace CafeChain.Data
{
    public class AppDbContext : DbContext
    {   
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // ========================= CUSTOMER =========================
        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountRole> AccountRoles { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<CustomerBank> CustomerBanks { get; set; }
        public DbSet<CustomerPhone> CustomerPhones { get; set; }
        public DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<RatingImage> RatingImages { get; set; }
        public DbSet<RatingReaction> RatingReactions { get; set; }

        // ========================= STAFF =========================
        public DbSet<Role> Roles { get; set; }
        public DbSet<ScopeType> ScopeTypes { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<StaffBank> StaffBanks { get; set; }
        public DbSet<StaffScope> StaffScopes { get; set; }
        public DbSet<StaffShift> StaffShifts { get; set; }
        public DbSet<StaffShiftStatus> StaffShiftStatuses { get; set; }
        public DbSet<AttendanceLog> AttendanceLogs { get; set; }
        public DbSet<StaffPhone> StaffPhones { get; set; }
        public DbSet<StaffAddress> StaffAddresses { get; set; }
        public DbSet<StaffDependent> StaffDependents { get; set; }

        // ========================= PERMISSION =========================
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AccountPermissionOverride> AccountPermissionOverrides { get; set; }

        // ========================= ORDER =========================
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderStatus> OrderStatuses { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }
        public DbSet<OrderTopping> OrderToppings { get; set; }
        public DbSet<InvoiceAuditLog> InvoiceAuditLogs { get; set; }
        public DbSet<OtpChallenge> OtpChallenges { get; set; }
        public DbSet<StaffNotification> StaffNotifications { get; set; }

        // ========================= DRINK =========================
        public DbSet<Drink> Drinks { get; set; }
        public DbSet<DrinkCategory> DrinkCategories { get; set; }
        public DbSet<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
        public DbSet<DrinkSizeToppingPolicy> DrinkSizeToppingPolicies { get; set; }
        public DbSet<DrinkSizeToppingPolicyAudit> DrinkSizeToppingPolicyAudits { get; set; }
        public DbSet<DrinkSizePriceAudit> DrinkSizePriceAudits { get; set; }
        public DbSet<PosCatalogState> PosCatalogStates { get; set; }
        public DbSet<DrinkImage> DrinkImages { get; set; }
        public DbSet<DrinkSize> DrinkSizes { get; set; }
        public DbSet<DrinkTopping> DrinkToppings { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeDetail> RecipeDetails { get; set; }
        public DbSet<Size> Sizes { get; set; }
        public DbSet<Topping> Toppings { get; set; }


        // ========================= STORE =========================
        public DbSet<Store> Stores { get; set; }
        public DbSet<StoreDrink> StoreDrinks { get; set; }
        public DbSet<StoreMenuItem> StoreMenuItems { get; set; }
        public DbSet<StoreMenuItemAudit> StoreMenuItemAudits { get; set; }
        public DbSet<StoreInventory> StoreInventories { get; set; }
        public DbSet<StoreTopping> StoreToppings { get; set; }
        public DbSet<StoreIP> StoreIPs { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<PosTerminal> PosTerminals { get; set; }
        public DbSet<DocumentNumberCounter> DocumentNumberCounters { get; set; }

        // ========================= INVENTORY =========================
        // Auditing
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<InventoryWriterModeTransition> InventoryWriterModeTransitions { get; set; }
        public DbSet<StoreInventoryWriterConfiguration> StoreInventoryWriterConfigurations { get; set; }

        // Costing
        public DbSet<InventoryCostLayer> InventoryCostLayers { get; set; }
        public DbSet<InventoryCostAllocation> InventoryCostAllocations { get; set; }
        public DbSet<ProductionCostAllocation> ProductionCostAllocations { get; set; }
        public DbSet<SalesCostAllocation> SalesCostAllocations { get; set; }
        public DbSet<SalesCostGap> SalesCostGaps { get; set; }
        public DbSet<InventoryNegativeCostGap> InventoryNegativeCostGaps { get; set; }
        public DbSet<InventoryCostGapSettlement> InventoryCostGapSettlements { get; set; }
        public DbSet<InventoryNegativeApproval> InventoryNegativeApprovals { get; set; }
        public DbSet<InventoryNegativeApprovalLine> InventoryNegativeApprovalLines { get; set; }
        public DbSet<InventoryTransferCostAllocation> InventoryTransferCostAllocations { get; set; }

        // Issue #134 — full-order cash refund
        public DbSet<CafeChain.Models.Inventories.Refunds.OrderRefund> OrderRefunds { get; set; }
        public DbSet<CafeChain.Models.Inventories.Refunds.RefundCostReversal> RefundCostReversals { get; set; }
        public DbSet<CafeChain.Models.Inventories.Refunds.RefundCostGap> RefundCostGaps { get; set; }

        // Documents
        public DbSet<InventoryDocument> InventoryDocuments { get; set; }
        public DbSet<InventoryDocumentDetail> InventoryDocumentDetails { get; set; }
        public DbSet<InventoryDocumentSnapshot> InventoryDocumentSnapshots { get; set; }
        public DbSet<InventoryDocumentSnapshotDetail> InventoryDocumentSnapshotDetails { get; set; }

        // Ingredients
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<UnitConversion> UnitConversions { get; set; }

        // Prepared items (BTP master — ADR-0006 / #116)
        public DbSet<PreparedItem> PreparedItems { get; set; }

        // Production runs (intent only — Issue #119 / 114B)
        public DbSet<ProductionRun> ProductionRuns { get; set; }

        // Issue #123 — legacy BTP consolidation runs / lines
        public DbSet<CafeChain.Models.Inventories.Consolidation.InventoryConsolidationRun> InventoryConsolidationRuns { get; set; }
        public DbSet<CafeChain.Models.Inventories.Consolidation.InventoryConsolidationLine> InventoryConsolidationLines { get; set; }

        // Stock
        public DbSet<StockAlert> StockAlerts { get; set; }
        public DbSet<StockAlertTransition> StockAlertTransitions { get; set; }
        public DbSet<RestockRequest> RestockRequests { get; set; }
        // Issue #128 — branch receipt + restock workflow
        public DbSet<BranchReceipt> BranchReceipts { get; set; }
        public DbSet<BranchReceiptLine> BranchReceiptLines { get; set; }
        public DbSet<RestockRequestFulfillment> RestockRequestFulfillments { get; set; }
        public DbSet<RestockFulfillmentPosting> RestockFulfillmentPostings { get; set; }
        public DbSet<RestockRequestTransition> RestockRequestTransitions { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<PurchaseOrderReceiptPosting> PurchaseOrderReceiptPostings { get; set; }
        public DbSet<PurchaseAdvice> PurchaseAdvices { get; set; }
        public DbSet<PurchaseAdviceLine> PurchaseAdviceLines { get; set; }
        public DbSet<PurchaseAdviceTransition> PurchaseAdviceTransitions { get; set; }
        public DbSet<PurchaseOrderBatch> PurchaseOrderBatches { get; set; }
        public DbSet<PurchaseOrderBatchLine> PurchaseOrderBatchLines { get; set; }
        public DbSet<PurchaseOrderLineAllocation> PurchaseOrderLineAllocations { get; set; }
        public DbSet<SupplierReceiptIssue> SupplierReceiptIssues { get; set; }
        public DbSet<SupplierReceiptIssueTransition> SupplierReceiptIssueTransitions { get; set; }

        // Stock Take
        public DbSet<StockTakeSession> StockTakeSessions { get; set; }
        public DbSet<StockTakeDetail> StockTakeDetails { get; set; }

        // Suppliers
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<IngredientSupplierPriceHistory> IngredientSupplierPriceHistories { get; set; }
        public DbSet<SupplierContact> SupplierContacts { get; set; }
        public DbSet<SupplierPhone> SupplierPhones { get; set; }
        public DbSet<IngredientSupplier> IngredientSuppliers { get; set; }
        public DbSet<SupplierStore> SupplierStores { get; set; }
        public DbSet<SupplierDuplicateWarning> SupplierDuplicateWarnings { get; set; }

        // Transactions
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        // Transfers
        public DbSet<InventoryTransfer> InventoryTransfers { get; set; }
        public DbSet<InventoryTransferDetail> InventoryTransferDetails { get; set; }
        public DbSet<RequestDeduplication> RequestDeduplications { get; set; }


        // ========================= PAYMENT =========================
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<PaymentStatus> PaymentStatuses { get; set; }
        public DbSet<CashSession> CashSessions { get; set; }
        public DbSet<TransactionLog> TransactionLogs { get; set; }

        // ========================= VOUCHER =========================
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherUsage> VoucherUsages { get; set; }
        public DbSet<OrderVoucher> OrderVouchers { get; set; }
        public DbSet<CustomerVoucher> CustomerVouchers { get; set; }
        public DbSet<WheelConfig> WheelConfigs { get; set; }
        public DbSet<WheelPrize> WheelPrizes { get; set; }
        public DbSet<WheelSpin> WheelSpins { get; set; }

        // ========================= LOCATION =========================
        public DbSet<Country> Countries { get; set; }
        public DbSet<Province> Provinces { get; set; }
        public DbSet<District> Districts { get; set; }   // 🔥 MỚI: Cấp Quận/Huyện
        public DbSet<Ward> Wards { get; set; }

        // ========================= LOYALTY =========================
        public DbSet<MemberLevel> MemberLevels { get; set; }
        public DbSet<PointTransaction> PointTransactions { get; set; }
        public DbSet<PointTransactionType> PointTransactionTypes { get; set; }

        public DbSet<SystemSetting> SystemSettings { get; set; }

        // ========================= CONFIGURATION =========================
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RevenueDto>().HasNoKey();
            modelBuilder.Entity<RevenueByStoreDto>().HasNoKey();
            modelBuilder.Entity<TopDrinkDto>().HasNoKey();
            modelBuilder.Entity<TopToppingDto>().HasNoKey();
            modelBuilder.Entity<PaymentMethodDto>().HasNoKey();
            modelBuilder.Entity<StaffPerformanceDto>().HasNoKey();
            modelBuilder.Entity<InventoryDto>().HasNoKey();
            modelBuilder.Entity<WasteDto>().HasNoKey();
            modelBuilder.Entity<CashFlowDto>().HasNoKey();
            modelBuilder.Entity<DashboardSummaryDto>().HasNoKey();

            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

    }
}
