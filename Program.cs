using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Application.Services;
using CafeChain.Application.Services.Accounts;
using CafeChain.Application.Services.Customers;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Infrastrusture.Repositories.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Repositories.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Services.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Services.Admin.Vouchers;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Repositories.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Services.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Infrastrusture.Repositories.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.Application.Services.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Application.Services.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Repositories.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings;
using CafeChain.Infrastrusture.Repositories.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Application.Services.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Services.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Repositories.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Infrastrusture.Repositories.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Services.Admin.Ingredients;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Infrastrusture.Repositories.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Application.Services.Customers;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Security;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Repositories.Admin.InventoryDocuments;


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


// =======================
// 6. Dependency Injection for Services and Repositories
// =======================

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

// Admin Staff
builder.Services.AddScoped<IAdminStaffRepository, AdminStaffRepository>();
builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();
// Admin Ingredients
builder.Services.AddScoped<IAdminIngredientRepository, AdminIngredientRepository>();
builder.Services.AddScoped<IAdminIngredientService, AdminIngredientService>();

// Admin Suppliers
builder.Services.AddScoped<IAdminSupplierRepository, AdminSupplierRepository>();
builder.Services.AddScoped<IAdminSupplierService, AdminSupplierService>();

// Admin Inventory Documents
builder.Services.AddScoped<IAdminInventoryDocumentRepository, AdminInventoryDocumentRepository>();
builder.Services.AddScoped<IAdminInventoryDocumentService, AdminInventoryDocumentService>();

// Security
builder.Services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// =======================
// Localization (vi-VN)
// =======================
var cultureInfo = new System.Globalization.CultureInfo("vi-VN");
// Xử lý rủi ro 1: Ép NumberFormat về InvariantCulture để tránh lỗi gõ/lưu số thập phân (do vi-VN dùng ',')
cultureInfo.NumberFormat = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultureInfo),
    SupportedCultures = new[] { cultureInfo },
    SupportedUICultures = new[] { cultureInfo }
};
app.UseRequestLocalization(localizationOptions);

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// === 🚀 DIAGNOSTIC SCRIPT (TỰ ĐỘNG CHẨN ĐOÁN LỖI DATABASE) ===
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CafeChain.Data.AppDbContext>();
    var conn = dbContext.Database.GetDbConnection();
    try
    {
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
