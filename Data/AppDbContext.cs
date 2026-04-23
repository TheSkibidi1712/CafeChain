using CafeChain.Models; 
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories;
using CafeChain.Models.Locations;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data.Configurations;
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
        public DbSet<CustomerAddress> CustomersAddresses { get; set; }
        public DbSet<CustomerBank> CustomerBanks { get; set; }
        public DbSet<CustomerPoint> CustomerPoints { get; set; }
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
        public DbSet<StaffPhone> StaffPhones { get; set; }
        public DbSet<StaffAddress> StaffAddresses { get; set; }
        public DbSet<StaffDependent> StaffDependents { get; set; }

        // ========================= ORDER =========================
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderStatus> OrderStatuses { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }
        public DbSet<OrderTopping> OrderToppings { get; set; }

        // ========================= DRINK =========================
        public DbSet<Drink> Drinks { get; set; }
        public DbSet<DrinkCategory> DrinkCategories { get; set; }
        public DbSet<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
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
        public DbSet<StoreInventory> StoreInventories { get; set; }
        public DbSet<StoreTopping> StoreToppings { get; set; }
        public DbSet<StoreIP> StoreIPs { get; set; }

        // ========================= INVENTORY =========================
        public DbSet<InventoryDebt> InventoryDebts { get; set; }
        public DbSet<InventoryDocument> InventoryDocuments { get; set; }
        public DbSet<InventoryDocumentDetail> InventoryDocumentDetails { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryTransfer> InventoryTransfers { get; set; }
        public DbSet<InventoryTransferDetail> InventoryTransferDetails { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<IngredientSupplier> IngredientSuppliers { get; set; }
        // Supplier
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierBankAccount> SupplierBankAccounts { get; set; }
        public DbSet<SupplierContact> SupplierContacts { get; set; }
        public DbSet<SupplierPhone> SupplierPhones { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<UnitConversion> UnitConversions { get; set; }


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
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

    }
}
