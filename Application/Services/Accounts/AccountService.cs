using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using System;
using System.Collections.Generic;
using System.Linq;
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

            // ===== CHECK DUPLICATE =====
            if (await _accountRepository.EmailExistsAsync(dto.Email))
                errors.Add("Email đã tồn tại");

            if (await _accountRepository.PhoneExistsAsync(dto.PhoneNumber))
                errors.Add("SĐT đã tồn tại");

            if (errors.Any())
            {
                return ServiceResult.Failure("Đăng ký thất bại", errors);
            }

            // ===== CREATE ACCOUNT =====
            var account = new Account
            {
                Email = dto.Email,
                PasswordHash = HashPassword(dto.Password),

                Customer = new Customer
                {
                    FullName = string.IsNullOrWhiteSpace(dto.FullName)
                        ? dto.Email
                        : dto.FullName,

                    DateOfBirth = dto.DateOfBirth, // ✅ thêm DOB

                    AvatarUrl = "/Images/Upload/avtdf.jpg",
                    Active = true
                }
            };

            await _accountRepository.CreateCustomerAccountAsync(account, dto.PhoneNumber);

            return ServiceResult.Success("Đăng ký thành công");
        }

        public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto)
        {
            var account = await _accountRepository.GetAccountByEmailAsync(dto.Email);

            if (account == null)
            {
                return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, account.PasswordHash))
            {
                return ServiceResult<LoginResponseDto>.Failure("Email hoặc mật khẩu không chính xác.");
            }

            if (!account.Active)
            {
                return ServiceResult<LoginResponseDto>.Failure("Tài khoản của bạn hiện đang bị tạm khóa.");
            }

            var response = new LoginResponseDto
            {
                Email = account.Email,
                FullName = account.AccountTypeId == 1 ? account.Customer?.FullName : account.Staff?.FullName,
                Role = account.AccountType?.Name ?? (account.AccountTypeId == 1 ? "Customer" : "Staff"),
                AccountId = account.AccountId,
                CustomerId = account.CustomerId,
                StaffId = account.StaffId,
                // 🔥 THÊM DÒNG NÀY ĐỂ LẤY LINK ẢNH TỪ DATABASE NHÉT VÀO HỘP 🔥
                AvatarUrl = account.Customer?.AvatarUrl
            };

            // Set a default name if both are null
            if (string.IsNullOrEmpty(response.FullName))
            {
                response.FullName = account.Email.Split('@')[0];
            }

            return ServiceResult<LoginResponseDto>.Success(response, "Đăng nhập thành công!");
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}
