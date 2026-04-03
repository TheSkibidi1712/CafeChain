namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    public class AdminSupplierDTO
    {
        public int SupplierId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public decimal DebtAmount { get; set; }
        public bool Active { get; set; }
    }
}
