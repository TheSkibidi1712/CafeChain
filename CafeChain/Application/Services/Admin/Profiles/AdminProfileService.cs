using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Admin.Profiles;
using CafeChain.Application.Interfaces.Admin.Profiles;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Profiles;
using CafeChain.Models.Enums.Cloudinaries;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Profile;

namespace CafeChain.Application.Services.Admin.Profiles;

public sealed class AdminProfileService : IAdminProfileService
{
    private readonly IAdminProfileRepository _repository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly ILogger<AdminProfileService> _logger;

    public AdminProfileService(
        IAdminProfileRepository repository,
        ICloudinaryService cloudinaryService,
        ILogger<AdminProfileService> logger)
    {
        _repository = repository;
        _cloudinaryService = cloudinaryService;
        _logger = logger;
    }

    public async Task<MyProfileVM?> GetMyProfileAsync(int accountId)
    {
        var staff = await _repository.GetByAccountIdAsync(accountId);
        if (staff == null) return null;

        return new MyProfileVM
        {
            FullName = staff.FullName,
            Email = staff.Account.Email,
            CCCD = staff.CCCD,
            DateOfBirth = staff.DateOfBirth,
            Gender = staff.Gender,
            RoleName = staff.Account.AccountRoles.Select(x => x.Role.Name).FirstOrDefault() ?? "Chưa phân quyền",
            StoreName = staff.Store?.Name ?? "Chưa phân chi nhánh",
            EmployeeStatus = staff.EmployeeStatus,
            Active = staff.Active,
            StartDate = staff.StartDate,
            AvatarUrl = string.IsNullOrWhiteSpace(staff.AvatarUrl)
                ? DefaultImages.StaffAvatarUrl
                : staff.AvatarUrl,
            PhoneNumber = staff.StaffPhones.FirstOrDefault(x => x.IsDefault)?.Phone
                ?? staff.StaffPhones.FirstOrDefault()?.Phone
        };
    }

    public async Task<ServiceResult<AdminProfileUpdateResult>> UpdateMyProfileAsync(
        int accountId,
        UpdateProfileVM model)
    {
        var staff = await _repository.GetByAccountIdAsync(accountId);
        if (staff == null)
            return ServiceResult<AdminProfileUpdateResult>.Failure("Không tìm thấy hồ sơ nhân viên.");

        var phone = model.PhoneNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(phone)
            && await _repository.PhoneExistsAsync(phone, staff.StaffId))
        {
            return ServiceResult<AdminProfileUpdateResult>.Failure(
                "Số điện thoại này đã được sử dụng bởi nhân viên khác.");
        }

        var previousPublicId = staff.AvatarPublicId;
        CafeChain.Application.DTOs.Common.UploadImageResult? uploadedAvatar = null;
        if (model.AvatarFile is { Length: > 0 })
        {
            try
            {
                uploadedAvatar = await _cloudinaryService.UploadAsync(
                    model.AvatarFile,
                    ImageFolder.Staffs,
                    ImageCategory.Avatar);
            }
            catch (Exception ex)
            {
                return ServiceResult<AdminProfileUpdateResult>.Failure(ex.Message);
            }
        }

        try
        {
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var defaultPhone = staff.StaffPhones.FirstOrDefault(x => x.IsDefault);
                    if (defaultPhone != null)
                    {
                        defaultPhone.Phone = phone;
                    }
                    else
                    {
                        staff.StaffPhones.Add(new StaffPhone
                        {
                            StaffId = staff.StaffId,
                            Phone = phone,
                            IsDefault = true
                        });
                    }
                }

                if (uploadedAvatar != null)
                {
                    staff.AvatarUrl = uploadedAvatar.Url;
                    staff.AvatarPublicId = uploadedAvatar.PublicId;
                }

                await _repository.SaveChangesAsync();
            });
        }
        catch (Exception ex)
        {
            await DeleteAvatarBestEffortAsync(uploadedAvatar?.PublicId);
            _logger.LogError(ex, "Cập nhật hồ sơ nhân viên {StaffId} thất bại.", staff.StaffId);
            return ServiceResult<AdminProfileUpdateResult>.Failure("Không thể cập nhật hồ sơ nhân viên.");
        }

        if (uploadedAvatar != null)
            await DeleteAvatarBestEffortAsync(previousPublicId);

        return ServiceResult<AdminProfileUpdateResult>.Success(
            new AdminProfileUpdateResult
            {
                AvatarChanged = uploadedAvatar != null,
                AvatarUrl = string.IsNullOrWhiteSpace(staff.AvatarUrl)
                    ? DefaultImages.StaffAvatarUrl
                    : staff.AvatarUrl
            },
            "Cập nhật hồ sơ thành công!");
    }

    private async Task DeleteAvatarBestEffortAsync(string? publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId)
            || string.Equals(publicId, DefaultImages.StaffAvatarPublicId, StringComparison.Ordinal)
            || string.Equals(publicId, "staffs/default-avatar", StringComparison.Ordinal))
            return;

        try
        {
            await _cloudinaryService.DeleteAsync(publicId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể xóa avatar Cloudinary {PublicId}.", publicId);
        }
    }
}
