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
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Services.Accounts;
using CafeChain.Infrastrusture.Repositories.Accounts;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Services;
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
using CafeChain.Infrastrusture.Interfaces.Admin.Staff;
using CafeChain.Infrastrusture.Repositories.Admin.Staff;
using CafeChain.Application.Interfaces.Admin.Staff;
using CafeChain.Application.Services.Admin.Staff;

var builder = WebApplication.CreateBuilder(args);

// =======================
// 1. MVC
// =======================
builder.Services.AddControllersWithViews();

// =======================
// 2. Database
// =======================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")).UseLazyLoadingProxies()
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
// 6. Dependency Injection for Services and Repositories
// =======================

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

// Admin Staff
builder.Services.AddScoped<IAdminStaffRepository, AdminStaffRepository>();
builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();

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

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
