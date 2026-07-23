using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments.Detail
{
    public class AdminInventoryDocumentDetailVM
    {
        public int InventoryDocumentId { get; set; }

        public string Code { get; set; }

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentStatus Status { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public DateTime DocumentDate { get; set; }

        public string? RequestKey { get; set; }

        public bool IsProcessing { get; set; }

        public string StoreName { get; set; }

        public string StaffName { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByName { get; set; }

        public InventoryPartnerType PartnerType { get; set; }

        public string? PartnerName { get; set; }

        public string? SupplierName { get; set; }

        public decimal? TotalAmount { get; set; }

        public decimal? VatAmount { get; set; }

        public decimal? FinalAmount { get; set; }

        public string? Note { get; set; }

        public bool AllowNegativeStock { get; set; }

        public string? NegativeReason { get; set; }

        public AdminInventoryNegativeApprovalVM? NegativeApproval { get; set; }

        public bool CanReviewNegativeApproval { get; set; }

        public string? NegativeApprovalReviewMessage { get; set; }

        public List<AdminInventoryCostGapVM> CostGaps { get; set; } = [];

        public List<AdminInventoryDocumentDetailItemVM>
            Details
        { get; set; } = [];
    }

    public class AdminInventoryNegativeApprovalVM
    {
        public long ApprovalId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string? ApproverName { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
        public string PolicyVersion { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public List<AdminInventoryNegativeApprovalLineVM> Lines { get; set; } = [];
    }

    public class AdminInventoryNegativeApprovalLineVM
    {
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal IssueQty { get; set; }
        public decimal ProjectedAfterQty { get; set; }
        public decimal EffectiveMaxNegativeQty { get; set; }
    }

    public class AdminInventoryCostGapVM
    {
        public long GapId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal OriginalQuantity { get; set; }
        public decimal OutstandingQuantity { get; set; }
        public decimal SettledQuantity { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
