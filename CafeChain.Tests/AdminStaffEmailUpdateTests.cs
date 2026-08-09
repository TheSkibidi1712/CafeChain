using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.Extensions.Logging;
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
                new Claim(ClaimTypes.Role, RoleConstants.BusinessOwner),
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
                Account = new Account
                {
                    AccountId = AccountId,
                    Email = email,
                    PasswordHash = passwordHash,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    AccountRoles = new List<AccountRole>
                    {
                        new AccountRole { AccountId = AccountId, RoleId = 8 } // Ca trưởng (ShiftSupervisor)
                    }
                },
                StaffScopes = new List<StaffScope>
                {
                    new StaffScope { StaffId = StaffId, ScopeTypeId = 5, ScopeRefId = StoreId }
                },
                StaffPhones = new List<StaffPhone>(),
                StaffAddresses = new List<StaffAddress>
                {
                    new StaffAddress
                    {
                        StaffId = StaffId,
                        Address = "123 Duong So 4",
                        ProvinceId = 1,
                        WardId = 3,
                        IsDefault = true
                    }
                },
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
                StoreId = StoreId,
                SelectedRoleId = 8, // Ca trưởng (ShiftSupervisor)
                ScopeTypeId = 5,
                ScopeRefId = StoreId,
                Phones = new List<string>(),
                ProvinceId = 1,
                WardId = 3,
                Address = "123 Duong So 4",
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
            repo.Setup(r => r.CCCDExistsAsync(It.IsAny<string>(), StaffId)).ReturnsAsync(false);
            repo.Setup(r => r.IsAddressHierarchyValidAsync(1, 3)).ReturnsAsync(true);
            repo.Setup(r => r.UpdateStaffProfileTransactionAsync(
                    It.IsAny<Staff>(),
                    It.IsAny<Account>(),
                    It.IsAny<List<StaffPhone>>(),
                    It.IsAny<List<StaffAddress>>()))
                .Callback<Staff, Account, List<StaffPhone>, List<StaffAddress>>(
                    (s, account, phones, addresses) =>
                    {
                        capture.Value = new CapturedUpdate(s, account, new List<AccountRole>());
                    })
                .Returns(Task.CompletedTask);

            var cloudinary = new Mock<ICloudinaryService>();
            var scope = new Mock<IScopeAuthorizationService>();
            var logger = new Mock<ILogger<AdminStaffService>>();
            var service = new AdminStaffService(repo.Object, cloudinary.Object, scope.Object, logger.Object);
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
            repo.Verify(r => r.UpdateStaffProfileTransactionAsync(
                It.IsAny<Staff>(),
                It.IsAny<Account>(),
                It.IsAny<List<StaffPhone>>(),
                It.IsAny<List<StaffAddress>>()), Times.Once);
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
            repo.Verify(r => r.UpdateStaffProfileTransactionAsync(
                It.IsAny<Staff>(),
                It.IsAny<Account>(),
                It.IsAny<List<StaffPhone>>(),
                It.IsAny<List<StaffAddress>>()), Times.Never);
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
