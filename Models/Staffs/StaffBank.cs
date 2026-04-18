using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Staffs
{
    public class StaffBank
    {
        public int StaffBankId { get; set; }
        public int? StaffId { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountHolderName { get; set; }
        public virtual Staff? Staff { get; set; }
    }
}
