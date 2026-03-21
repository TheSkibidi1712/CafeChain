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
namespace CafeChain.Data
{
    public class AppDbContext : DbContext
    {   
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // ========================= CUSTOMER =========================
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<CustomerBank> CustomerBanks { get; set; }
        public DbSet<CustomerPoint> CustomerPoints { get; set; }
        public DbSet<CustomerPhone> CustomerPhones { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountType> AccountTypes { get; set; }

        // ========================= STAFF =========================
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<StaffBank> StaffBanks { get; set; }
        public DbSet<StaffRole> StaffRoles { get; set; }
        public DbSet<StaffScope> StaffScopes { get; set; }
        public DbSet<ScopeType> ScopeTypes { get; set; }
        public DbSet<StaffShift> StaffShifts { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Role> Roles { get; set; }

        // ========================= ORDER =========================
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderStatus> OrderStatuses { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }
        public DbSet<OrderTopping> OrderToppings { get; set; }

        // ========================= DRINK =========================
        public DbSet<Drink> Drinks { get; set; }
        public DbSet<DrinkCategory> DrinkCategories { get; set; }
        public DbSet<DrinkImage> DrinkImages { get; set; }
        public DbSet<DrinkSize> DrinkSizes { get; set; }
        public DbSet<DrinkTopping> DrinkToppings { get; set; }
        public DbSet<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
        public DbSet<Size> Sizes { get; set; }
        public DbSet<Topping> Toppings { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeDetail> RecipeDetails { get; set; }

        // ========================= STORE =========================
        public DbSet<Store> Stores { get; set; }
        public DbSet<StoreDrink> StoreDrinks { get; set; }
        public DbSet<StoreInventory> StoreInventories { get; set; }
        public DbSet<StoreTopping> StoreToppings { get; set; }

        // ========================= INVENTORY =========================
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryTransactionType> InventoryTransactionTypes { get; set; }
        public DbSet<StockImport> StockImports { get; set; }
        public DbSet<StockImportDetail> StockImportDetails { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }

        // ========================= PAYMENT =========================
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<PaymentStatus> PaymentStatuses { get; set; }
        public DbSet<CashSession> CashSessions { get; set; }

        // ========================= VOUCHER =========================
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherUsage> VoucherUsages { get; set; }
        public DbSet<OrderVoucher> OrderVouchers { get; set; }

        // ========================= LOCATION =========================
        public DbSet<Country> Countries { get; set; }
        public DbSet<Province> Provinces { get; set; }
        public DbSet<Ward> Wards { get; set; }

        // ========================= LOYALTY =========================
        public DbSet<MemberLevel> MemberLevels { get; set; }
        public DbSet<PointTransaction> PointTransactions { get; set; }
        public DbSet<PointTransactionType> PointTransactionTypes { get; set; }

        // ========================= CONFIG =========================
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        }
    }
}
