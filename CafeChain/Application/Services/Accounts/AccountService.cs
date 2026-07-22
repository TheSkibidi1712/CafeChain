using CafeChain.Application.Constants;
using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Customer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Accounts
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;

        public AccountService(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public async Task<ServiceResult> RegisterCustomerAsync(RegisterDto dto)
        {
            try
            {
                var errors = new List<string>();

                // ===== NORMALIZE =====
                dto.Email = dto.Email?.Trim().ToLower();
                dto.PhoneNumber = dto.PhoneNumber?.Trim();
                dto.FullName = dto.FullName?.Trim();

                // ===== VALIDATE =====
                if (string.IsNullOrWhiteSpace(dto.Email))
                {
                    errors.Add("Email không hợp lệ");
                }

                if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    errors.Add("SĐT không hợp lệ");
                }

                if (string.IsNullOrWhiteSpace(dto.Password))
                {
                    errors.Add("Mật khẩu không hợp lệ");
                }

                if (errors.Any())
                {
                    return ServiceResult.Failure("Đăng ký thất bại", errors);
                }

                // ===== EMAIL EXISTS =====
                var emailExists = await _accountRepository.EmailExistsAsync(dto.Email);

                if (emailExists)
                {
                    errors.Add("Email đã tồn tại");
                    return ServiceResult.Failure("Đăng ký thất bại", errors);
                }

                // ===== CHECK PHONE =====
                var customerPhone = await _accountRepository.GetCustomerPhoneAsync(dto.PhoneNumber);

                // =========================================================
                // CASE 1:
                // CUSTOMER ĐÃ TỒN TẠI TỪ POS
                // =========================================================
                if (customerPhone != null)
                {
                    var existingCustomer = customerPhone.Customer;

                    // Đã có account rồi
                    if (existingCustomer.AccountId.HasValue)
                    {
                        errors.Add("SĐT đã được đăng ký tài khoản");
                        return ServiceResult.Failure("Đăng ký thất bại", errors);
                    }

                    // =====================================
                    // GÁN AVATAR MẶC ĐỊNH NẾU CHƯA CÓ
                    // =====================================
                    if (string.IsNullOrWhiteSpace(existingCustomer.AvatarUrl))
                    {
                        existingCustomer.AvatarUrl = DefaultImages.CustomerAvatarUrl;

                        existingCustomer.AvatarPublicId = DefaultImages.CustomerAvatarPublicId;
                    }


                    // ========================================================
                    // Gán Password Hash và tạo account mới cho customer này
                    // ========================================================
                    var passwordHash = HashPassword(dto.Password);


                    await _accountRepository.CreateAccountForExistingCustomerAsync(existingCustomer, dto.Email, passwordHash);

                    return ServiceResult.Success("Liên kết tài khoản thành công. Điểm thưởng và voucher của bạn đã được giữ nguyên.");
                }

                // =========================================================
                // CASE 2:
                // CUSTOMER MỚI HOÀN TOÀN
                // =========================================================
                var account = new Account
                {
                    Email = dto.Email,
                    PasswordHash = HashPassword(dto.Password),
                    Active = true,
                    CreatedAt = DateTime.Now,

                    Customer = new Customer
                    {
                        FullName = dto.FullName ?? "Khách hàng mới",

                        DateOfBirth = dto.DateOfBirth,

                        AvatarUrl = DefaultImages.CustomerAvatarUrl,

                        AvatarPublicId = DefaultImages.CustomerAvatarPublicId,

                        Gender = dto.Gender,

                        Category = CustomerCategory.Registered,

                        Active = true,

                        CreatedAt = DateTime.Now,

                        CustomerCode = $"CUS{DateTime.Now.Ticks}"
                    }
                };

                await _accountRepository.CreateNewCustomerAccountAsync(account, dto.PhoneNumber);

                return ServiceResult.Success("Đăng ký thành công");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure(
                    "Lỗi hệ thống khi đăng ký",
                    new List<string> { ex.Message });
            }
        }


        public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto)
        {
            try
            {
                dto.Email = dto.Email?.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                {
                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không hợp lệ");
                }

                var account = await _accountRepository.GetAccountByEmailAsync(dto.Email);

                if (account == null)
                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");

                if (!account.Active)
                    return ServiceResult<LoginResponseDto>.Failure("Tài khoản đã bị khóa.");

                // ===== 🔥 RESET LOCK NẾU ĐÃ HẾT HẠN =====
                if (account.LockoutEnd.HasValue && account.LockoutEnd <= DateTime.UtcNow)
                {
                    await _accountRepository.ExecuteInTransactionAsync(
                        async () =>
                        {
                            account.LockoutEnd = null;
                            account.FailedLoginAttempts = 0;

                            await _accountRepository.UpdateAsync(account);
                    });
                }

                // ===== LOCK =====
                if (account.LockoutEnd.HasValue && account.LockoutEnd > DateTime.UtcNow)
                {
                    var remain = (account.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;

                    var result = ServiceResult<LoginResponseDto>.Failure("Tài khoản bị khóa", errorCode: "LOCKED");

                    result.Data = new LoginResponseDto
                    {
                        IsLocked = true,
                        LockRemainingMinutes = (int)Math.Ceiling(remain)
                    };

                    return result;
                }

                // ===== PASSWORD =====
                if (!BCrypt.Net.BCrypt.Verify(dto.Password, account.PasswordHash))
                {
                    await _accountRepository.ExecuteInTransactionAsync(
                    async () =>
                    {
                        account.FailedLoginAttempts++;

                        if (account.FailedLoginAttempts >= 5)
                        {
                            account.LockoutEnd =
                                DateTime.UtcNow.AddMinutes(15);

                            account.FailedLoginAttempts = 0;
                        }

                        await _accountRepository.UpdateAsync(account);
                    });

                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");
                }

                // ===== SUCCESS =====
                await _accountRepository.ExecuteInTransactionAsync(
                async () =>
                {
                    account.FailedLoginAttempts = 0;

                    account.LockoutEnd = null;

                    await _accountRepository.UpdateAsync(account);
                });

                var allRoles = account.AccountRoles.Select(r => r.Role.Name).ToList();

                var rolePriority = new[]
                {
                    RoleConstants.SystemAdmin,
                    RoleConstants.BusinessOwner,
                    RoleConstants.AreaManager,
                    RoleConstants.StoreManager,
                    RoleConstants.ShiftSupervisor,
                    RoleConstants.AccountantWarehouse,
                    RoleConstants.SalesStaff,
                    RoleConstants.Customer
                };
                string primaryRole = rolePriority.FirstOrDefault(allRoles.Contains)
                    ?? allRoles.FirstOrDefault()
                    ?? RoleConstants.Customer;

                var fullName = account.Customer?.FullName
                               ?? account.Staff?.FullName
                               ?? account.Email.Split('@')[0];

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
                    new Claim(ClaimTypes.Name, fullName),
                    new Claim(ClaimTypes.Email, account.Email),
                };

                foreach (var role in allRoles)
                    claims.Add(new Claim(ClaimTypes.Role, role));

                if (!allRoles.Any())
                    claims.Add(new Claim(ClaimTypes.Role, RoleConstants.Customer));

                if (account.Customer?.CustomerId != null)
                    claims.Add(new Claim("CustomerId", account.Customer.CustomerId.ToString()));

                if (account.Staff?.StaffId != null)
                    claims.Add(new Claim("StaffId", account.Staff.StaffId.ToString()));

                if (account.Staff?.StoreId != null)
                    claims.Add(new Claim("StoreId", account.Staff.StoreId.ToString()));

                var avatarUrl = account.Customer != null
                    ? (string.IsNullOrWhiteSpace(account.Customer.AvatarUrl)
                        ? DefaultImages.CustomerAvatarUrl
                        : account.Customer.AvatarUrl)
                    : (string.IsNullOrWhiteSpace(account.Staff?.AvatarUrl)
                        ? DefaultImages.StaffAvatarUrl
                        : account.Staff.AvatarUrl);

                claims.Add(new Claim("AvatarUrl", avatarUrl));

                return ServiceResult<LoginResponseDto>.Success(new LoginResponseDto
                {
                    Email = account.Email,
                    FullName = fullName,
                    Role = primaryRole,
                    AllRoles = allRoles,
                    AccountId = account.AccountId,
                    CustomerId = account.Customer?.CustomerId,
                    StaffId = account.Staff?.StaffId,
                    StoreId = account.Staff?.StoreId,
                    AvatarUrl = avatarUrl,
                    Claims = claims
                }, "Đăng nhập thành công!");
            }
            catch (Exception ex)
            {
                return ServiceResult<LoginResponseDto>.Failure("Lỗi hệ thống: " + ex.Message);
            }
        }

        public async Task<ServiceResult> ChangeRequiredPasswordAsync(int accountId, string oldPassword, string newPassword)
        {
            if (accountId <= 0)
                return ServiceResult.Failure("Không xác định được tài khoản.");
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return ServiceResult.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");

            var account = await _accountRepository.GetAccountByIdAsync(accountId);
            if (account == null || !account.Active)
                return ServiceResult.Failure("Tài khoản không tồn tại hoặc đã bị khóa.");
            if (!account.RequiresPasswordChange)
                return ServiceResult.Failure("Tài khoản không yêu cầu đổi mật khẩu lần đầu.");
            if (string.IsNullOrWhiteSpace(account.PasswordHash)
                || !BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
                return ServiceResult.Failure("Mật khẩu hiện tại không chính xác.");

            await _accountRepository.ExecuteInTransactionAsync(() =>
            {
                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                account.RequiresPasswordChange = false;
                return _accountRepository.UpdateAsync(account);
            });
            return ServiceResult.Success("Đổi mật khẩu thành công.");
        }

        public async Task<ServiceResult> ChangePasswordAsync(int accountId, string oldPassword, string newPassword)
        {
            if (accountId <= 0)
                return ServiceResult.Failure("Không xác định được tài khoản.");
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return ServiceResult.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");
            if (string.Equals(oldPassword, newPassword, StringComparison.Ordinal))
                return ServiceResult.Failure("Mật khẩu mới không được trùng mật khẩu hiện tại.");

            var account = await _accountRepository.GetAccountByIdAsync(accountId);
            if (account == null || !account.Active)
                return ServiceResult.Failure("Tài khoản không tồn tại hoặc đã bị khóa.");
            if (string.IsNullOrWhiteSpace(account.PasswordHash)
                || !BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
                return ServiceResult.Failure("Mật khẩu hiện tại không chính xác.");

            await _accountRepository.ExecuteInTransactionAsync(() =>
            {
                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                account.RequiresPasswordChange = false;
                return _accountRepository.UpdateAsync(account);
            });

            return ServiceResult.Success("Đổi mật khẩu thành công!");
        }

        public async Task<(bool IsLocked, int RemainingMinutes)> CheckLockAsync(string email)
        {
            var account = await _accountRepository.GetAccountByEmailAsync(email);

            if (account == null)
                return (false, 0);

            if (account.LockoutEnd.HasValue && account.LockoutEnd > DateTime.UtcNow)
            {
                var remain = (account.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;

                return (true, (int)Math.Ceiling(remain));
            }

            return (false, 0);
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}
