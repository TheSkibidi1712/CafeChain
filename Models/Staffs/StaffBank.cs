using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Staffs
{
    public class StaffBank
    {
        public int StaBId { get; set; }
        public int? StaId { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public virtual Staff? Staff { get; set; }
    }
}
