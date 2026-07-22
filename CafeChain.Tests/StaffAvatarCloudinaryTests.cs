using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Common;
using CafeChain.Application.Interfaces.Admin.Profiles;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.Profiles;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastructure.Interfaces.Admin.Profiles;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Cloudinaries;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.ViewModels.Profile;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests;

public sealed class StaffAvatarCloudinaryTests
{
    [Fact]
    public async Task Create_without_avatar_uses_staff_default_and_does_not_upload()
    {
        var repository = CreateStaffRepository();
        Staff? captured = null;
        repository.Setup(x => x.CreateStaffTransactionAsync(
                It.IsAny<Staff>(), It.IsAny<Account>(), It.IsAny<List<AccountRole>>(),
                It.IsAny<List<StaffScope>>(), It.IsAny<List<StaffPhone>>(), It.IsAny<List<StaffAddress>>()))
            .Callback<Staff, Account, List<AccountRole>, List<StaffScope>, List<StaffPhone>, List<StaffAddress>>(
                (staff, _, _, _, _, _) => captured = staff)
            .Returns(Task.CompletedTask);
        var cloudinary = new Mock<ICloudinaryService>(MockBehavior.Strict);
        var service = CreateStaffService(repository, cloudinary);

        var result = await service.CreateStaffAsync(CreateStaffModel(), CreateOwner(), file: null);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(captured);
        Assert.Equal(DefaultImages.StaffAvatarUrl, captured!.AvatarUrl);
        Assert.Equal(DefaultImages.StaffAvatarPublicId, captured.AvatarPublicId);
        cloudinary.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_with_avatar_persists_cloudinary_url_and_public_id()
    {
        var repository = CreateStaffRepository();
        Staff? captured = null;
        repository.Setup(x => x.CreateStaffTransactionAsync(
                It.IsAny<Staff>(), It.IsAny<Account>(), It.IsAny<List<AccountRole>>(),
                It.IsAny<List<StaffScope>>(), It.IsAny<List<StaffPhone>>(), It.IsAny<List<StaffAddress>>()))
            .Callback<Staff, Account, List<AccountRole>, List<StaffScope>, List<StaffPhone>, List<StaffAddress>>(
                (staff, _, _, _, _, _) => captured = staff)
            .Returns(Task.CompletedTask);
        var cloudinary = new Mock<ICloudinaryService>();
        cloudinary.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), ImageFolder.Staffs, ImageCategory.Avatar))
            .ReturnsAsync(new UploadImageResult { Url = "https://cloudinary.test/staff.jpg", PublicId = "cafechain/staffs/avatar_1" });
        var service = CreateStaffService(repository, cloudinary);

        var result = await service.CreateStaffAsync(CreateStaffModel(), CreateOwner(), CreateImage());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("https://cloudinary.test/staff.jpg", captured!.AvatarUrl);
        Assert.Equal("cafechain/staffs/avatar_1", captured.AvatarPublicId);
        cloudinary.Verify(x => x.UploadAsync(It.IsAny<IFormFile>(), ImageFolder.Staffs, ImageCategory.Avatar), Times.Once);
    }

    [Fact]
    public async Task Admin_profile_replaces_avatar_and_deletes_previous_custom_image_after_commit()
    {
        var staff = CreateExistingStaff();
        var repository = new Mock<IAdminProfileRepository>();
        repository.Setup(x => x.GetByAccountIdAsync(staff.AccountId)).ReturnsAsync(staff);
        repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns((Func<Task> operation) => operation());
        repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
        var cloudinary = new Mock<ICloudinaryService>();
        cloudinary.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), ImageFolder.Staffs, ImageCategory.Avatar))
            .ReturnsAsync(new UploadImageResult { Url = "https://cloudinary.test/new.jpg", PublicId = "cafechain/staffs/new" });
        cloudinary.Setup(x => x.DeleteAsync("cafechain/staffs/old")).ReturnsAsync(true);
        var service = new AdminProfileService(repository.Object, cloudinary.Object, NullLogger<AdminProfileService>.Instance);

        var result = await service.UpdateMyProfileAsync(
            staff.AccountId,
            new UpdateProfileVM { AvatarFile = CreateImage() });

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data.AvatarChanged);
        Assert.Equal("https://cloudinary.test/new.jpg", staff.AvatarUrl);
        Assert.Equal("cafechain/staffs/new", staff.AvatarPublicId);
        cloudinary.Verify(x => x.DeleteAsync("cafechain/staffs/old"), Times.Once);
    }

    [Fact]
    public async Task Admin_profile_database_failure_deletes_new_upload_but_keeps_old_image()
    {
        var staff = CreateExistingStaff();
        var repository = new Mock<IAdminProfileRepository>();
        repository.Setup(x => x.GetByAccountIdAsync(staff.AccountId)).ReturnsAsync(staff);
        repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .ThrowsAsync(new InvalidOperationException("database failed"));
        var cloudinary = new Mock<ICloudinaryService>();
        cloudinary.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), ImageFolder.Staffs, ImageCategory.Avatar))
            .ReturnsAsync(new UploadImageResult { Url = "https://cloudinary.test/new.jpg", PublicId = "cafechain/staffs/new" });
        cloudinary.Setup(x => x.DeleteAsync("cafechain/staffs/new")).ReturnsAsync(true);
        var service = new AdminProfileService(repository.Object, cloudinary.Object, NullLogger<AdminProfileService>.Instance);

        var result = await service.UpdateMyProfileAsync(
            staff.AccountId,
            new UpdateProfileVM { AvatarFile = CreateImage() });

        Assert.False(result.IsSuccess);
        cloudinary.Verify(x => x.DeleteAsync("cafechain/staffs/new"), Times.Once);
        cloudinary.Verify(x => x.DeleteAsync("cafechain/staffs/old"), Times.Never);
    }

    [Fact]
    public async Task Admin_profile_never_deletes_shared_default_avatar()
    {
        var staff = CreateExistingStaff();
        staff.AvatarUrl = DefaultImages.StaffAvatarUrl;
        staff.AvatarPublicId = DefaultImages.StaffAvatarPublicId;
        var repository = new Mock<IAdminProfileRepository>();
        repository.Setup(x => x.GetByAccountIdAsync(staff.AccountId)).ReturnsAsync(staff);
        repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns((Func<Task> operation) => operation());
        repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
        var cloudinary = new Mock<ICloudinaryService>();
        cloudinary.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), ImageFolder.Staffs, ImageCategory.Avatar))
            .ReturnsAsync(new UploadImageResult { Url = "https://cloudinary.test/new.jpg", PublicId = "cafechain/staffs/new" });
        var service = new AdminProfileService(repository.Object, cloudinary.Object, NullLogger<AdminProfileService>.Instance);

        var result = await service.UpdateMyProfileAsync(
            staff.AccountId,
            new UpdateProfileVM { AvatarFile = CreateImage() });

        Assert.True(result.IsSuccess, result.Message);
        cloudinary.Verify(x => x.DeleteAsync(DefaultImages.StaffAvatarPublicId), Times.Never);
    }

    private static Mock<IAdminStaffRepository> CreateStaffRepository()
    {
        var repository = new Mock<IAdminStaffRepository>();
        repository.Setup(x => x.ScopeCoversStoreAsync(5, 1, 1)).ReturnsAsync(true);
        repository.Setup(x => x.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        repository.Setup(x => x.IsAddressHierarchyValidAsync(1, 2, 3)).ReturnsAsync(true);
        return repository;
    }

    private static AdminStaffService CreateStaffService(
        Mock<IAdminStaffRepository> repository,
        Mock<ICloudinaryService> cloudinary)
    {
        return new AdminStaffService(
            repository.Object,
            cloudinary.Object,
            Mock.Of<IScopeAuthorizationService>(),
            NullLogger<AdminStaffService>.Instance);
    }

    private static StaffCreateVM CreateStaffModel() => new()
    {
        FullName = "Nhân viên test",
        Email = "avatar.staff@test.local",
        Password = "CafeChain@123",
        StoreId = 1,
        SelectedRoleId = 4,
        ScopeTypeId = 5,
        ScopeRefId = 1,
        ProvinceId = 1,
        DistrictId = 2,
        WardId = 3,
        Address = "123 Đường Test"
    };

    private static ClaimsPrincipal CreateOwner() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, RoleConstants.BusinessOwner),
        new Claim("StaffId", "1"),
        new Claim("StoreId", "1")
    }, "Test"));

    private static IFormFile CreateImage()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        return new FormFile(stream, 0, stream.Length, "AvatarFile", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private static Staff CreateExistingStaff() => new()
    {
        StaffId = 20,
        AccountId = 30,
        StoreId = 1,
        FullName = "Nhân viên profile",
        AvatarUrl = "https://cloudinary.test/old.jpg",
        AvatarPublicId = "cafechain/staffs/old",
        Account = new Account
        {
            AccountId = 30,
            Email = "profile@test.local",
            AccountRoles = new List<AccountRole>()
        },
        StaffPhones = new List<StaffPhone>()
    };
}
