using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.ViewModels.Customers;
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
    }
}