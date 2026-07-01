using CafeChain.Application.DTOs.Admin.InventoryDocuments.Index;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;
using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.InventoryDocuments.Index
{
    
    public class AdminInventoryDocumentIndexVM
    {
        // =========================
        // FILTER
        // =========================

        public AdminInventoryDocumentFilterDTO Filter { get; set; }
            = new();

        // =========================
        // LIST
        // =========================

        public PaginatedListViewModel<AdminInventoryDocumentListVM>
            Documents
        { get; set; } = null!;

        // =========================
        // DASHBOARD
        // =========================

        public int TotalDocuments { get; set; }

        public int DraftDocuments { get; set; }

        public int ConfirmedDocuments { get; set; }

        public int CancelledDocuments { get; set; }

        public int ThisMonthDocuments { get; set; }

        // =========================
        // DROPDOWN
        // =========================

        public IEnumerable<StoreDropdownVM> Stores { get; set; }  = new List<StoreDropdownVM>();
    }
}
