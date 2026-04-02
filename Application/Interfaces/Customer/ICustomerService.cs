using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.ViewModels.Customers;
using CafeChain.Models.Locations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Customers
{
    public interface ICustomerService
    {
        // DÒNG NÀY LÀ ĐỂ SỬA LỖI 2 NÈ BÁC:
        Task<(string Url, bool IsReused)> UpdateAvatarAsync(int customerId, IFormFile file);
        Task<CustomerProfileViewModel> GetCustomerProfileAsync(string accountId);
        Task<bool> UpdateProfileAsync(int customerId, UpdateProfileRequest request);
        // Dòng cần thêm:
        Task<(bool Success, string Message)> ChangePasswordAsync(int accountId, ChangePasswordViewModel request);

        Task<CafeChain.Models.Customers.Customer> GetByPhoneAsync(string phone);
        Task<(bool Success, string Message, int CustomerId)> QuickRegisterAsync(string fullName, string phone);
        
        // Load Location từ DB theo yêu cầu mới
        Task<List<Province>> GetProvincesAsync();
        Task<List<Ward>> GetWardsByProvinceAsync(int provinceId);
    }
}