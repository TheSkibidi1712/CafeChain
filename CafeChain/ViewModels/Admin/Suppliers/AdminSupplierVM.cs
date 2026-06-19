using CafeChain.Application.DTOs.Admin.Suppliers;

namespace CafeChain.ViewModels.Admin.Suppliers
{
    public class AdminSupplierIndexVM
    {
        public List<AdminSupplierDTO> Suppliers { get; set; } = new();
        public string? Search { get; set; }
        public bool? Status { get; set; }
    }

    public class AdminSupplierDetailVM
    {
        public AdminSupplierDetailDTO Supplier { get; set; } = new();
    }
}
