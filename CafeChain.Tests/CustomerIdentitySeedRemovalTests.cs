using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Services.Accounts;
using CafeChain.Infrastrusture.Repositories.Accounts;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Customer;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

public sealed class CustomerIdentitySeedRemovalTests : IntegrationTestBase
{
    [Fact]
    public async Task New_customer_registration_creates_no_persisted_role_and_login_derives_customer_claim()
    {
        using var context = CreateDbContext();
        var service = new AccountService(new AccountRepository(context));

        var registered = await service.RegisterCustomerAsync(new RegisterDto
        {
            FullName = "Khách hàng tự đăng ký",
            Email = "customer-no-role@test.local",
            PhoneNumber = "0912345678",
            Password = "CafeChain@123"
        });

        Assert.True(registered.IsSuccess, registered.Message);
        var account = await context.Accounts.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.AccountRoles)
            .SingleAsync(x => x.Email == "customer-no-role@test.local");
        Assert.NotNull(account.Customer);
        Assert.Empty(account.AccountRoles);

        var login = await service.LoginAsync(new LoginDto
        {
            Email = account.Email,
            Password = "CafeChain@123"
        });

        Assert.True(login.IsSuccess, login.Message);
        Assert.Equal(RoleConstants.Customer, login.Data!.Role);
        Assert.Contains(RoleConstants.Customer, login.Data.AllRoles);
        Assert.Contains(login.Data.Claims, claim =>
            claim.Type == ClaimTypes.Role && claim.Value == RoleConstants.Customer);
        Assert.Contains(login.Data.Claims, claim =>
            claim.Type == "CustomerId" && claim.Value == account.Customer!.CustomerId.ToString());
    }

    [Fact]
    public async Task Existing_pos_customer_can_link_account_without_customer_role_row()
    {
        using var context = CreateDbContext();
        var customer = new Customer
        {
            CustomerCode = "POS-CUSTOMER-NO-ROLE",
            FullName = "Khách POS",
            Category = CustomerCategory.Guest,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        customer.CustomerPhones.Add(new CustomerPhone
        {
            Phone = "0987654321",
            IsDefault = true
        });
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var service = new AccountService(new AccountRepository(context));
        var registered = await service.RegisterCustomerAsync(new RegisterDto
        {
            FullName = customer.FullName,
            Email = "linked-pos-customer@test.local",
            PhoneNumber = "0987654321",
            Password = "CafeChain@456"
        });

        Assert.True(registered.IsSuccess, registered.Message);
        var linked = await context.Customers.AsNoTracking()
            .Include(x => x.Account)
            .ThenInclude(x => x!.AccountRoles)
            .SingleAsync(x => x.CustomerId == customer.CustomerId);
        Assert.NotNull(linked.Account);
        Assert.Equal(CustomerCategory.Registered, linked.Category);
        Assert.Empty(linked.Account!.AccountRoles);
    }

    [Fact]
    public async Task Account_without_customer_or_roles_is_not_promoted_to_customer()
    {
        using var context = CreateDbContext();
        context.Accounts.Add(new Account
        {
            Email = "orphan-account@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CafeChain@789"),
            Active = true,
            CreatedAt = DateTime.UtcNow,
            AccountRoles = []
        });
        await context.SaveChangesAsync();

        var service = new AccountService(new AccountRepository(context));
        var login = await service.LoginAsync(new LoginDto
        {
            Email = "orphan-account@test.local",
            Password = "CafeChain@789"
        });

        Assert.True(login.IsSuccess, login.Message);
        Assert.Equal("Chưa phân quyền", login.Data!.Role);
        Assert.Empty(login.Data.AllRoles);
        Assert.DoesNotContain(login.Data.Claims, claim =>
            claim.Type == ClaimTypes.Role && claim.Value == RoleConstants.Customer);
        Assert.DoesNotContain(login.Data.Claims, claim => claim.Type == "CustomerId");
    }

    [Fact]
    public void Production_seed_sources_contain_no_customer_identity_fixture()
    {
        var accountConfiguration = Read("CafeChain", "Data", "Configurations", "Customers", "Accounts", "AccountConfiguration.cs");
        var customerConfiguration = Read("CafeChain", "Data", "Configurations", "Customers", "CustomerInfos", "CustomerConfiguration.cs");
        var seedAll = Read("CafeChain", "Scripts", "SeedAll.sql");
        var initialMigration = Read("CafeChain", "Migrations", "20260815152712_InitialCreate.cs");
        var snapshot = Read("CafeChain", "Migrations", "AppDbContextModelSnapshot.cs");

        foreach (var source in new[] { accountConfiguration, customerConfiguration, initialMigration, snapshot })
        {
            Assert.DoesNotContain("khachhang@gmail.com", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CUS000111", source, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("(7,N'Khách hàng'", seedAll, StringComparison.Ordinal);
        Assert.DoesNotContain("(7,7)", seedAll, StringComparison.Ordinal);
        Assert.DoesNotContain("@Coverage17Customer", seedAll, StringComparison.Ordinal);
        Assert.Contains("(4,N'CUSTOMER',N'Quản lý khách hàng'", seedAll, StringComparison.Ordinal);
        Assert.Contains("PointTransactionTypes", initialMigration, StringComparison.Ordinal);
        Assert.Contains("Vouchers", initialMigration, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. path]));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
