namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentFormVM
    {
        public List<LookupItemVM> Stores { get; set; } = new();
        public List<LookupItemVM> Staffs { get; set; } = new();
        public List<LookupItemVM> Suppliers { get; set; } = new();
        public List<LookupItemVM> Ingredients { get; set; } = new();
        public List<LookupItemVM> Units { get; set; } = new();
    }
}
