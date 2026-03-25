using CafeChain.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Repositories.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Services.Admin.Categories;

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
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// =======================
// 5. HttpContextAccessor
// =======================
builder.Services.AddHttpContextAccessor();

// =======================
// 6. Admin Area Services
// =======================
builder.Services.AddScoped<IAdminCategoryRepository, AdminCategoryRepository>();
builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();

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
