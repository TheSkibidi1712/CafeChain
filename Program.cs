using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services;
using CafeChain.Application.Services.Accounts;
using CafeChain.Application.Services.Admin.Categories;
using CafeChain.Application.Services.Admin.Dashboard;
using CafeChain.Application.Services.Admin.Drinks;
using CafeChain.Application.Services.Admin.DrinkSizes;
using CafeChain.Application.Services.Admin.DrinkToppings;
using CafeChain.Application.Services.Admin.Ingredients;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Application.Services.Admin.InventoryTransfers;
using CafeChain.Application.Services.Admin.Sizes;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Admin.Toppings;
using CafeChain.Application.Services.Admin.Vouchers;
using CafeChain.Application.Services.Cart;
using CafeChain.Application.Services.Customers;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Infrastrusture.Configurations;
using CafeChain.Infrastrusture.Interfaces.Accounts;
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
using CafeChain.Infrastrusture.Repositories.Accounts;
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
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);

// =======================
// 1. MVC
// =======================
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });


builder.Services.AddSignalR();
// =======================
// 2. Database
// =======================
// Trong Program.cs, khi cấu hình DbContext:
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(120) // 120 giây
    )
    .UseLazyLoadingProxies()
);


// =======================
// 3. SESSION
// =======================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =======================
// 4. Authentication (COOKIE)
// =======================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 🔥 HTTPS only
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// =======================
// 5. HttpContextAccessor
// =======================
builder.Services.AddHttpContextAccessor();

// =======================
// 5.1 Authorization Policies
// =======================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminPanelAccess", policy =>
        policy.RequireRole(
            CafeChain.Application.Constants.RoleConstants.SuperAdmin,
            CafeChain.Application.Constants.RoleConstants.CEO,
            CafeChain.Application.Constants.RoleConstants.CFO,
            CafeChain.Application.Constants.RoleConstants.MarketingManager,
            CafeChain.Application.Constants.RoleConstants.OperationsManager,
            CafeChain.Application.Constants.RoleConstants.HRManager,
            CafeChain.Application.Constants.RoleConstants.AreaManager,
            CafeChain.Application.Constants.RoleConstants.StoreManager
        ));
});

// ======================
// 5.2 Cloudinary Configuration
// ======================
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("Cloudinary"));


// =======================
// 6. Dependency Injection for Services and Repositories
// =======================

// Cloudinary
builder.Services.AddSingleton(sp =>
{
    var settings = sp
        .GetRequiredService<IOptions<CloudinarySettings>>()
        .Value;

    var account = new Account(
        settings.CloudName,
        settings.ApiKey,
        settings.ApiSecret);

    var cloudinary = new Cloudinary(account);

    cloudinary.Api.Secure = true;

    return cloudinary;
});

// Admin Category
builder.Services.AddScoped<IAdminCategoryRepository, AdminCategoryRepository>();
builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();

// Account
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Home
builder.Services.AddScoped<IDrinkService, DrinkService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Inventory Abstraction
builder.Services.AddScoped<IInventoryService, CafeChain.Application.Services.Inventory.InventoryService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.Inventories.IInventoryDeductionService, CafeChain.Application.Services.Inventories.InventoryDeductionService>();

// Workers
builder.Services.AddHostedService<CafeChain.Application.Workers.OrderCleanupWorker>();
builder.Services.AddHostedService<CafeChain.Application.Services.Workers.PaymentCleanupWorker>();

// PayOS Integration
builder.Services.AddScoped<CafeChain.Application.Services.PayOSIntegration.IPayOSService, CafeChain.Application.Services.PayOSIntegration.PayOSService>();

// Đăng ký FileService
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// Bản đồ Geocoding
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();

// Admin Sizes
builder.Services.AddScoped<IAdminSizeRepository, AdminSizeRepository>();
builder.Services.AddScoped<IAdminSizeService, AdminSizeService>();

//Admin DrinkSizes
builder.Services.AddScoped<IAdminDrinkSizeRepository, AdminDrinkSizeRepository>();
builder.Services.AddScoped<IAdminDrinkSizeService, AdminDrinkSizeService>();

// Admin Toppings
builder.Services.AddScoped<IAdminToppingRepository, AdminToppingRepository>();
builder.Services.AddScoped<IAdminToppingService, AdminToppingService>();

// Admin DrinkToppings
builder.Services.AddScoped<IAdminDrinkToppingRepository, AdminDrinkToppingRepository>();
builder.Services.AddScoped<IAdminDrinkToppingService, AdminDrinkToppingService>();

// Admin Drinks
builder.Services.AddScoped<IAdminDrinkRepository, AdminDrinkRepository>();
builder.Services.AddScoped<IAdminDrinkService, AdminDrinkService>();

// Admin Voucher
builder.Services.AddScoped<IAdminVoucherService, AdminVoucherService>();
builder.Services.AddScoped<IAdminWheelService, AdminWheelService>();

// Admin Staff
builder.Services.AddScoped<IAdminStaffRepository, AdminStaffRepository>();
builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();
// Admin Ingredients
builder.Services.AddScoped<IAdminIngredientRepository, AdminIngredientRepository>();
builder.Services.AddScoped<IAdminIngredientService, AdminIngredientService>();

// Admin Recipes (BOM Module)
builder.Services.AddScoped<CafeChain.Application.Interfaces.Admin.Recipes.IAdminRecipeService, CafeChain.Application.Services.Admin.Recipes.AdminRecipeService>();


// Admin Inventory Documents
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<IAdminInventoryDocumentRepository, AdminInventoryDocumentRepository>();
builder.Services.AddScoped<IAdminInventoryDocumentService, AdminInventoryDocumentService>();

// Admin Store Inventories
builder.Services.AddScoped<IAdminStoreInventoryRepository, AdminStoreInventoryRepository>();
builder.Services.AddScoped<IAdminStoreInventoryService, AdminStoreInventoryService>();

// Admin Suppliers
builder.Services.AddScoped<IAdminSupplierRepository, AdminSupplierRepository>();
builder.Services.AddScoped<IAdminSupplierService, AdminSupplierService>();

// Admin Orders Dashboard
builder.Services.AddScoped<CafeChain.Application.Interfaces.Admin.IAdminOrderService, CafeChain.Application.Services.Admin.AdminOrderService>();

// Admin Inventory Transfers
builder.Services.AddScoped<IAdminInventoryTransferRepository, AdminInventoryTransferRepository>();
builder.Services.AddScoped<IAdminInventoryTransferService, AdminInventoryTransferService>();

// Security
builder.Services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.Attendance.IAttendanceSecurityService, CafeChain.Application.Services.Attendance.AttendanceSecurityService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.Attendance.IAttendanceActionService, CafeChain.Application.Services.Attendance.AttendanceActionService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.Admin.Staffs.IAdminStaffShiftService, CafeChain.Application.Services.Admin.Staffs.AdminStaffShiftService>();

// Admin Dashboard
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Interlock HR & POS
builder.Services.AddScoped<CafeChain.Application.Interfaces.Attendance.IHrAttendanceService, CafeChain.Application.Services.Attendance.HrAttendanceService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.POS.IWorkShiftService, CafeChain.Application.Services.POS.WorkShiftService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.POS.ISupervisorAuthService, CafeChain.Application.Services.POS.SupervisorAuthService>();
builder.Services.AddScoped<CafeChain.Application.Interfaces.POS.IPOSOrderService, CafeChain.Application.Services.POS.POSOrderService>();

// POS Repositories
builder.Services.AddScoped<CafeChain.Infrastrusture.Interfaces.Admin.POS.IPOSOrderRepository, CafeChain.Infrastrusture.Repositories.Admin.POS.POSOrderRepository>();
builder.Services.AddScoped<CafeChain.Infrastrusture.Interfaces.Admin.POS.ISupervisorRepository, CafeChain.Infrastrusture.Repositories.Admin.POS.SupervisorRepository>();

// [FIX] PayOS SSL Bypass & HttpClient Registration (Senior .NET Security Fix)
builder.Services.AddHttpClient("PayOS")
    .ConfigurePrimaryHttpMessageHandler((IServiceProvider sp) =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var handler = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        };

        // CHỈ BYPASS SSL Ở MÔI TRƯỜNG LOCAL/DEV ĐỂ FIX LỖI HANDSHAKE TRÊN WINDOWS
        if (env.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }

        return handler;
    });

builder.Services.AddSingleton(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("PayOS");
    
    return new Net.payOS.PayOS(
        config["PayOS:ClientId"], 
        config["PayOS:ApiKey"], 
        config["PayOS:ChecksumKey"]
        // Lưu ý: SDK Net.payOS hiện tại có thể không nhận HttpClient qua constructor tùy version, 
        // nhưng cấu hình này đảm bảo các request HTTP khác qua Factory sẽ an toàn.
    );
});
// Settings
builder.Services.AddScoped<CafeChain.Application.Interfaces.Admin.Settings.IAdminSettingService, CafeChain.Application.Services.Admin.Settings.AdminSettingService>();

// Trong Program.cs, chỗ builder.Services...
builder.Services.AddSingleton(new Net.payOS.PayOS(
    builder.Configuration["PayOS:ClientId"], 
    builder.Configuration["PayOS:ApiKey"], 
    builder.Configuration["PayOS:ChecksumKey"]
));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// =======================
// Localization (vi-VN)
// =======================
var cultureInfo = new System.Globalization.CultureInfo("vi-VN");
cultureInfo.NumberFormat = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultureInfo),
    SupportedCultures = new[] { cultureInfo },
    SupportedUICultures = new[] { cultureInfo }
};
app.UseRequestLocalization(localizationOptions);

// [FIX ROUTE] Đổi OrderHistory -> AdminOrderHistory theo yêu cầu
app.MapControllerRoute(
    name: "admin_order_history",
    pattern: "Admin/AdminOrderHistory",
    defaults: new { area = "Admin", controller = "AdminOrder", action = "History" });

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<OrderHub>("/orderHub");
app.MapHub<PaymentHub>("/paymentHub");

// === 🚀 DIAGNOSTIC SCRIPT (TỰ ĐỘNG CHẨN ĐOÁN LỖI DATABASE) ===
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CafeChain.Data.AppDbContext>();
    var conn = dbContext.Database.GetDbConnection();
    try
    {
        // Tự động Apply Migration
        dbContext.Database.Migrate();


        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.TABLE_NAME, 
                   CASE WHEN c.COLUMN_NAME IS NOT NULL THEN 'TRUE' ELSE 'FALSE' END AS HasActive
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN INFORMATION_SCHEMA.COLUMNS c 
                   ON t.TABLE_NAME = c.TABLE_NAME AND c.COLUMN_NAME = 'Active'
            WHERE t.TABLE_TYPE = 'BASE TABLE' 
              AND t.TABLE_NAME IN ('Drinks', 'DrinkSizes', 'Sizes', 'DrinkCategories', 'Ratings', 'Stores', 'DrinkToppings', 'Toppings', 'DrinkDefaultToppings', 'Customers', 'Accounts', 'Staffs', 'CustomerAddresses')
            ORDER BY HasActive DESC, t.TABLE_NAME;";
        using var reader = cmd.ExecuteReader();
        var outputPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "diagnostic.txt");
        using var writer = new System.IO.StreamWriter(outputPath, false);
        writer.WriteLine("--- SCHEMA DIAGNOSTIC ---");
        while (reader.Read())
        {
            writer.WriteLine($"{reader.GetString(0)}:{reader.GetString(1)}");
        }
        writer.WriteLine("--- END ---");
        Console.WriteLine("\n\n✅ ĐÃ GHI KẾT QUẢ VÀO FILE diagnostic.txt\n\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine("LỖI GHI FILE: " + ex.Message);
    }
}
// =============================================================

app.Run();
