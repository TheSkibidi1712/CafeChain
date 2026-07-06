// ==========================================
// Infrastructure - Interfaces
// ==========================================
using CafeChain.Infrastructure.Interfaces.Customers;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Infrastructure.Interfaces.Attendance;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Infrastructure.Interfaces.Admin.Permissions;
using CafeChain.Infrastrusture.Interfaces.Systems;



// ==========================================
// Infrastructure - Repositories
// ==========================================
using CafeChain.Infrastructure.Repositories.Customers;
using CafeChain.Infrastrusture.Repositories.Accounts;
using CafeChain.Infrastructure.Repositories.Attendance;
using CafeChain.Infrastrusture.Repositories.Admin.Categories;
using CafeChain.Infrastrusture.Repositories.Admin.Dashboard;
using CafeChain.Infrastrusture.Repositories.Admin.Drinks;
using CafeChain.Infrastrusture.Repositories.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Repositories.Admin.DrinkToppings;
using CafeChain.Infrastrusture.Repositories.Admin.Ingredients;
using CafeChain.Infrastrusture.Repositories.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Repositories.Admin.InventoryTransfers;
using CafeChain.Infrastrusture.Repositories.Admin.Sizes;
using CafeChain.Infrastrusture.Repositories.Admin.Staffs;
using CafeChain.Infrastrusture.Repositories.Admin.StoreInventories;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Infrastrusture.Repositories.Admin.Toppings;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.Permissions;
using CafeChain.Infrastrusture.Repositories.Systems;

namespace CafeChain.Extensions.Services
{
    public static class RepositoryServiceExtensions
    {
        public static IServiceCollection AddCafeChainRepositories(this IServiceCollection services)
        {
            // Account
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();

            // Customer
            services.AddScoped<ICustomerRepository, CustomerRepository>();

            // System
            services.AddScoped<IRequestDeduplicationRepository, RequestDeduplicationRepository>();
            services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

            // Admin - Category
            services.AddScoped<IAdminCategoryRepository, AdminCategoryRepository>();

            // Admin - Sizes
            services.AddScoped<IAdminSizeRepository, AdminSizeRepository>();

            // Admin - DrinkSizes
            services.AddScoped<IAdminDrinkSizeRepository, AdminDrinkSizeRepository>();

            // Admin - Toppings
            services.AddScoped<IAdminToppingRepository, AdminToppingRepository>();

            // Admin - DrinkToppings
            services.AddScoped<IAdminDrinkToppingRepository, AdminDrinkToppingRepository>();

            // Admin - Drinks
            services.AddScoped<IAdminDrinkRepository, AdminDrinkRepository>();

            // Admin - Staff
            services.AddScoped<IAdminStaffRepository, AdminStaffRepository>();

            // Admin - Ingredients
            services.AddScoped<IAdminIngredientRepository, AdminIngredientRepository>();

            // Admin - Inventory Documents
            services.AddScoped<IAdminInventoryDocumentRepository, AdminInventoryDocumentRepository>();

            // Admin - Inventory Transfers
            services.AddScoped<IAdminInventoryTransferRepository, AdminInventoryTransferRepository>();

            // Admin - Store Inventories
            services.AddScoped<IAdminStoreInventoryRepository, AdminStoreInventoryRepository>();

            // Admin - Suppliers
            services.AddScoped<IAdminSupplierRepository, AdminSupplierRepository>();

            // Dashboard
            services.AddScoped<IDashboardRepository, DashboardRepository>();

            // HR & Attendance
            services.AddScoped<IAttendanceRepository, AttendanceRepository>();
            services.AddScoped<IWorkShiftRepository, WorkShiftRepository>();

            // POS
            services.AddScoped<IPOSOrderRepository, POSOrderRepository>();
            services.AddScoped<ISupervisorRepository, SupervisorRepository>();

            // Permissions
            services.AddScoped<IAdminPermissionRepository, AdminPermissionRepository>();

            return services;
        }
    }
}
