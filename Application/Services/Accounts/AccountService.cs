using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
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

                // ===== ROLE =====
                var roleName = account.AccountRoles
                    .Select(r => r.Role.Name)
                    .FirstOrDefault() ?? "Customer";

                // ===== FULL NAME =====
                var fullName = account.Customer?.FullName
                               ?? account.Staff?.FullName
                               ?? account.Email.Split('@')[0];

                var response = new LoginResponseDto
                {
                    Email = account.Email,
                    FullName = fullName,
                    Role = roleName,
                    AccountId = account.AccountId,
                    CustomerId = account.Customer?.CustomerId,
                    StaffId = account.Staff?.StaffId,
                    AvatarUrl = account.Customer?.AvatarUrl
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
