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
        Task<string> UpdateAvatarAsync(int customerId, IFormFile file); 
        Task<CustomerProfileViewModel> GetCustomerProfileAsync(string accountId);
        Task<bool> UpdateProfileAsync(int customerId, UpdateProfileRequest request);
        Task<(bool Success, string Message)> ChangePasswordAsync(int accountId,ChangePasswordRequest request);
        Task<CafeChain.Models.Customers.Customer> GetByPhoneAsync(string phone);
        Task<(bool Success, string Message, int CustomerId)> QuickRegisterAsync(string fullName, string phone);
        
        Task<List<Province>> GetProvincesAsync();
        Task<List<District>> GetDistrictsByProvinceAsync(int provinceId);
        Task<List<Ward>> GetWardsByDistrictAsync(int districtId);
    }
}