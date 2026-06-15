using CafeChain.Models.Vouchers;
using System.Collections.Generic;

namespace CafeChain.ViewModels.Admin.Vouchers
{
    public class VoucherDashboardViewModel
    {
        public IEnumerable<Voucher> Vouchers { get; set; }
        public int TotalUsedCount { get; set; }
        public double ConversionRate { get; set; }
        public IEnumerable<CafeChain.Models.Loyalties.MemberLevel> MemberLevels { get; set; }
    }
}
