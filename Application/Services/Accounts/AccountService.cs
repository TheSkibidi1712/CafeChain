using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using CafeChain.Application.Constants;
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
                    errors.Add("Email không hợp lệ");

                if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                    errors.Add("SĐT không hợp lệ");

                if (string.IsNullOrWhiteSpace(dto.Password))
                    errors.Add("Mật khẩu không hợp lệ");

                if (errors.Any())
                    return ServiceResult.Failure("Đăng ký thất bại", errors);

                // ===== CHECK DUPLICATE (CHỈ 1 LẦN) =====
                var emailExists = await _accountRepository.EmailExistsAsync(dto.Email);
                var phoneExists = await _accountRepository.PhoneExistsAsync(dto.PhoneNumber);

                if (emailExists)
                    errors.Add("Email đã tồn tại");

                if (phoneExists)
                    errors.Add("SĐT đã tồn tại");

                if (errors.Any())
                    return ServiceResult.Failure("Đăng ký thất bại", errors);

                // ===== CREATE ACCOUNT =====
                var account = new Account
                {
                    Email = dto.Email,
                    PasswordHash = HashPassword(dto.Password),
                    Active = true,
                    CreatedAt = DateTime.Now,

                    Customer = new Customer
                    {
                        FullName = dto.FullName ?? dto.Email,
                        DateOfBirth = dto.DateOfBirth,
                        AvatarUrl = "/Images/Upload/avtdf.jpg",
                        Active = true,
                        CreatedAt = DateTime.Now
                    }
                };


                await _accountRepository.CreateCustomerAccountAsync(account, dto.PhoneNumber);

                return ServiceResult.Success("Đăng ký thành công");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đăng ký", new List<string> { ex.Message });
            }
        }

        public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto)
        {
            try
            {
                // ===== NORMALIZE =====
                dto.Email = dto.Email?.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                {
                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không hợp lệ");
                }

                var account = await _accountRepository.GetAccountByEmailAsync(dto.Email);

                if (account == null)
                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");

                if (!BCrypt.Net.BCrypt.Verify(dto.Password, account.PasswordHash))
                    return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");

                if (!account.Active)
                    return ServiceResult<LoginResponseDto>.Failure("Tài khoản bị khóa.");

                // ===== ROLE: Lấy TẤT CẢ roles =====
                var allRoles = account.AccountRoles
                    .Select(r => r.Role.Name)
                    .ToList();

                // 🔥 Chọn role ưu tiên cao nhất cho redirect
                // Thứ tự ưu tiên: Admin System > Store Manager > Ward/Province Manager > Cashier > Customer
                string primaryRole;
                if (allRoles.Any(r => r.Contains("Admin")))
                    primaryRole = allRoles.First(r => r.Contains("Admin"));
                else if (allRoles.Any(r => r.Contains("Manager")))
                    primaryRole = allRoles.First(r => r.Contains("Manager"));
                else if (allRoles.Any(r => r.Contains("Cashier")))
                    primaryRole = allRoles.First(r => r.Contains("Cashier"));
                else
                    primaryRole = allRoles.FirstOrDefault() ?? RoleConstants.Customer;

                // ===== FULL NAME =====
                var fullName = account.Customer?.FullName
                               ?? account.Staff?.FullName
                               ?? account.Email.Split('@')[0];

                // ===== BUILD CLAIMS (Centralized Logic) =====
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
                    new Claim(ClaimTypes.Name, fullName),
                    new Claim(ClaimTypes.Email, account.Email),
                };

                foreach (var roleName in allRoles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, roleName));
                }

                if (!allRoles.Any())
                {
                    claims.Add(new Claim(ClaimTypes.Role, RoleConstants.Customer));
                }

                if (account.Customer?.CustomerId != null)
                    claims.Add(new Claim("CustomerId", account.Customer.CustomerId.ToString()));

                if (account.Staff?.StaffId != null)
                    claims.Add(new Claim("StaffId", account.Staff.StaffId.ToString()));

                if (account.Staff?.StoreId != null)
                    claims.Add(new Claim("StoreId", account.Staff.StoreId.ToString()));

                var avatarUrl = account.Customer?.AvatarUrl ?? account.Staff?.AvatarUrl ?? "/Images/Upload/avtdf.jpg";
                claims.Add(new Claim("AvatarUrl", avatarUrl));

                var response = new LoginResponseDto
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
                    Claims = claims // 🔥 Trả về Claims hoàn chỉnh
                };

                return ServiceResult<LoginResponseDto>.Success(response, "Đăng nhập thành công!");
            }
            catch (Exception ex)
            {
                return ServiceResult<LoginResponseDto>.Failure("Lỗi hệ thống khi đăng nhập: " + ex.Message);
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}
