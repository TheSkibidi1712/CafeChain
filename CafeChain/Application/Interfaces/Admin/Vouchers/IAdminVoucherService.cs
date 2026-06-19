using CafeChain.Models.Vouchers;

namespace CafeChain.Application.Interfaces.Admin.Vouchers
{
    public interface IAdminVoucherService
    {
        Task<(bool Success, string Message, Voucher Voucher)> ValidateVoucherAsync(string code, int customerId, decimal subTotal);
        Task<decimal> CalculateMemberDiscountAsync(int customerId, decimal subTotal);
        Task<int> GetCustomerPointsAsync(int customerId);

        // Admin functions
        Task<IEnumerable<Voucher>> GetAllVouchersAsync();
        Task<Voucher> GetVoucherByIdAsync(int id);
        Task<bool> CreateVoucherAsync(Voucher voucher);
        Task<bool> UpdateVoucherAsync(Voucher voucher);
        Task<bool> ToggleVoucherActiveAsync(int id);

        // Stats
        Task<int> GetTotalUsageCountAsync();
        Task<double> GetConversionRateAsync();

        // Membership levels
        Task<IEnumerable<CafeChain.Models.Loyalties.MemberLevel>> GetAllMemberLevelsAsync();
        Task<bool> UpdateMemberLevelAsync(CafeChain.Models.Loyalties.MemberLevel level);
    }
}
