using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Enums.Inventory;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;

namespace CafeChain.ViewModels.Admin.InventoryDocuments.Create
{
    public class AdminInventoryDocumentCreateVM
    {
        // =====================================================
        // HEADER
        // =====================================================

        public string Code { get; set; } = string.Empty;

        public DateTime DocumentDate { get; set; }

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public int StoreId { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public int StaffId { get; set; }

        public string StaffName { get; set; } = string.Empty;

        public bool NegativeInventoryPolicyValid { get; set; }

        public bool NegativeInventoryPolicyEnabled { get; set; }

        public bool NegativeInventoryApprovalRequired { get; set; }

        // =====================================================
        // PARTNER
        // =====================================================

        public int? SupplierId { get; set; }

        public string? SupplierName { get; set; }

        // =====================================================
        // DETAIL
        // =====================================================

        public List<AdminInventoryDocumentCreateItemVM> Items
        { get; set; } = [];

        // =====================================================
        // DROPDOWN
        // =====================================================

        public IEnumerable<StoreDropdownVM> Stores
        { get; set; } = [];

        public IEnumerable<SupplierDropdownVM> Suppliers
        { get; set; } = [];

        // =====================================================
        // SUMMARY
        // =====================================================

        public InventoryCreateSummaryDTO Summary
        { get; set; } = new();

        public string? Note { get; set; }
    }
}
