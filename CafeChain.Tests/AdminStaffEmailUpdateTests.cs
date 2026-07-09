using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace CafeChain.Tests
{
    /// <summary>
    /// Admin staff email edit — allow real OTP test emails without hard-coding.
    /// Covers: successful email update, duplicate rejection, password unchanged when blank.
    /// </summary>
    public class AdminStaffEmailUpdateTests
    {
        private const int StaffId = 42;
        private const int AccountId = 77;
        private const int StoreId = 3;
        private const string OriginalEmail = "shift.supervisor@fake.local";
        private const string OriginalPasswordHash = "$2a$11$originalhashvalueplaceholderxx";

        private sealed record CapturedUpdate(Staff Staff, Account Account, List<AccountRole> Roles);

        private sealed class CaptureBox
        {
            public CapturedUpdate? Value { get; set; }
        }

        private static ClaimsPrincipal CreateSuperAdminPrincipal()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, RoleConstants.SuperAdmin),
                new Claim("StaffId", "1"),
                new Claim("StoreId", StoreId.ToString()),
            }, authenticationType: "Test");
            return new ClaimsPrincipal(identity);
        }

        private static Staff CreateExistingStaff(string email = OriginalEmail, string passwordHash = OriginalPasswordHash)
        {
            return new Staff
            {
                StaffId = StaffId,
                AccountId = AccountId,
                StoreId = StoreId,
                FullName = "Nguyễn Ca Trưởng",
                Active = true,
                BaseSalary = 10_000_000m,
                Account = new Account
                {
                    AccountId = AccountId,
                    Email = email,
                    PasswordHash = passwordHash,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    AccountRoles = new List<AccountRole>
                    {
                        new AccountRole { AccountId = AccountId, RoleId = 9 } // Shift Supervisor
                    }
                },
                StaffScopes = new List<StaffScope>
                {
                    new StaffScope { StaffId = StaffId, ScopeTypeId = 5, ScopeRefId = StoreId }
                },
                StaffPhones = new List<StaffPhone>(),
                StaffAddresses = new List<StaffAddress>(),
                StaffBanks = new List<StaffBank>(),
            };
        }

        private static StaffEditVM CreateEditModel(string email, string? newPassword = null)
        {
            return new StaffEditVM
            {
                StaffId = StaffId,
                AccountId = AccountId,
                FullName = "Nguyễn Ca Trưởng",
                Email = email,
                NewPassword = newPassword,
                BaseSalary = 10_000_000m,
                StoreId = StoreId,
                SelectedRoleId = 9,
                ScopeTypeId = 5,
                ScopeRefId = StoreId,
                Phones = new List<string>(),
                Addresses = new List<string>(),
                Banks = new List<StaffBankVM>(),
                Dependents = new List<StaffDependentVM>(),
                CurrentAvatarUrl = "/Images/avatars/avtdf.jpg",
                Active = true
            };
        }

        private static (AdminStaffService Service, Mock<IAdminStaffRepository> Repo, CaptureBox Capture) CreateHarness(
            Staff? existingStaff = null,
            bool emailExists = false)
        {
            var staff = existingStaff ?? CreateExistingStaff();
            var repo = new Mock<IAdminStaffRepository>(MockBehavior.Strict);
            var capture = new CaptureBox();

            repo.Setup(r => r.GetStaffByIdAsync(StaffId)).ReturnsAsync(staff);
            repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), AccountId)).ReturnsAsync(emailExists);
            repo.Setup(r => r.DefaultPhoneExistsAsync(It.IsAny<string>(), StaffId)).ReturnsAsync(false);
            repo.Setup(r => r.TaxCodeExistsAsync(It.IsAny<string>(), StaffId)).ReturnsAsync(false);
            repo.Setup(r => r.CCCDExistsAsync(It.IsAny<string>(), StaffId)).ReturnsAsync(false);
            repo.Setup(r => r.UpdateStaffTransactionAsync(
                    It.IsAny<Staff>(),
                    It.IsAny<Account>(),
                    It.IsAny<List<AccountRole>>(),
                    It.IsAny<List<StaffScope>>(),
                    It.IsAny<List<StaffPhone>>(),
                    It.IsAny<List<StaffAddress>>(),
                    It.IsAny<List<StaffBank>>(),
                    It.IsAny<List<StaffDependent>>()))
                .Callback<Staff, Account, List<AccountRole>, List<StaffScope>, List<StaffPhone>, List<StaffAddress>, List<StaffBank>, List<StaffDependent>>(
                    (s, account, roles, scopes, phones, addresses, banks, dependents) =>
                    {
                        capture.Value = new CapturedUpdate(s, account, roles);
                    })
                .Returns(Task.CompletedTask);

            var env = new Mock<IWebHostEnvironment>();
            var scope = new Mock<IScopeAuthorizationService>();
            var service = new AdminStaffService(repo.Object, env.Object, scope.Object);
            return (service, repo, capture);
        }

        [Fact]
        public async Task UpdateStaff_EmailChanged_PersistsTrimmedEmailAndKeepsPassword()
        {
            var (service, repo, capture) = CreateHarness();
            var admin = CreateSuperAdminPrincipal();
            var model = CreateEditModel("  real.supervisor@gmail.com  ");

            var result = await service.UpdateStaffAsync(model, admin, file: null!);

            Assert.True(result.IsSuccess, result.Message);
            Assert.NotNull(capture.Value);
            Assert.Equal("real.supervisor@gmail.com", capture.Value!.Account.Email);
            Assert.Equal(OriginalPasswordHash, capture.Value.Account.PasswordHash);
            repo.Verify(r => r.EmailExistsAsync("real.supervisor@gmail.com", AccountId), Times.Once);
            repo.Verify(r => r.UpdateStaffTransactionAsync(
                It.IsAny<Staff>(),
                It.IsAny<Account>(),
                It.IsAny<List<AccountRole>>(),
                It.IsAny<List<StaffScope>>(),
                It.IsAny<List<StaffPhone>>(),
                It.IsAny<List<StaffAddress>>(),
                It.IsAny<List<StaffBank>>(),
                It.IsAny<List<StaffDependent>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateStaff_DuplicateEmail_RejectedWithVietnameseMessage()
        {
            var (service, repo, capture) = CreateHarness(emailExists: true);
            var admin = CreateSuperAdminPrincipal();
            var model = CreateEditModel("taken@example.com");

            var result = await service.UpdateStaffAsync(model, admin, file: null!);

            Assert.False(result.IsSuccess);
            Assert.Equal("Email đã tồn tại trong hệ thống.", result.Message);
            Assert.Null(capture.Value);
            repo.Verify(r => r.UpdateStaffTransactionAsync(
                It.IsAny<Staff>(),
                It.IsAny<Account>(),
                It.IsAny<List<AccountRole>>(),
                It.IsAny<List<StaffScope>>(),
                It.IsAny<List<StaffPhone>>(),
                It.IsAny<List<StaffAddress>>(),
                It.IsAny<List<StaffBank>>(),
                It.IsAny<List<StaffDependent>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStaff_EmptyNewPassword_DoesNotChangePasswordHash()
        {
            var staff = CreateExistingStaff();
            var (service, _, capture) = CreateHarness(staff);
            var admin = CreateSuperAdminPrincipal();
            var model = CreateEditModel("new.email@example.com", newPassword: "   ");

            var result = await service.UpdateStaffAsync(model, admin, file: null!);

            Assert.True(result.IsSuccess, result.Message);
            Assert.NotNull(capture.Value);
            Assert.Equal(OriginalPasswordHash, capture.Value!.Account.PasswordHash);
            Assert.Equal("new.email@example.com", capture.Value.Account.Email);
        }
    }
}
