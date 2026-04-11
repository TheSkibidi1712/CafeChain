using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentCreateVM
    {
        public InventoryDocumentVM Form { get; set; } = new();

        public List<Store> Stores { get; set; } = new();
        public List<Staff> Staffs { get; set; } = new();
        public List<Supplier> Suppliers { get; set; } = new();

        public List<IngredientDropdownDTO> Ingredients { get; set; } = new();
        public List<UnitDropdownDTO> Units { get; set; } = new();
    }
}
