using CafeChain.Models.Vouchers;
using System.Collections.Generic;
using CafeChain.Application.DTOs.Customer;

namespace CafeChain.ViewModels.Customers
{
    public class MyVouchersViewModel
    {
        public CustomerProfileViewModel Profile { get; set; }
        public List<CustomerVoucher> ValidVouchers { get; set; } = new List<CustomerVoucher>();
        public List<CustomerVoucher> UsedVouchers { get; set; } = new List<CustomerVoucher>();
        public List<CustomerVoucher> ExpiredVouchers { get; set; } = new List<CustomerVoucher>();
    }
}
