using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Procurement
{
    public class PurchaseAdvice
    {
        public int PurchaseAdviceId { get; set; }
        public string AdviceNumber { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public int RequestedByStaffId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime NeededByDate { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public int? ReviewedByStaffId { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public int? RejectedByStaffId { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public int? CancelledByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Staff RequestedByStaff { get; set; } = null!;
        public virtual Staff? ReviewedByStaff { get; set; }
        public virtual Staff? RejectedByStaff { get; set; }
        public virtual Staff? CancelledByStaff { get; set; }
        public virtual ICollection<PurchaseAdviceLine> Lines { get; set; } = new List<PurchaseAdviceLine>();
        public virtual ICollection<PurchaseAdviceTransition> Transitions { get; set; } = new List<PurchaseAdviceTransition>();
    }

    public class PurchaseAdviceLine
    {
        public int PurchaseAdviceLineId { get; set; }
        public int PurchaseAdviceId { get; set; }
        public int RestockRequestId { get; set; }
        public int IngredientId { get; set; }
        public decimal RequestedPurchaseBaseQuantity { get; set; }
        public decimal AllocatedToPoBaseQuantity { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal ClosedBaseQuantity { get; set; }
        public int BaseUnitId { get; set; }
        public decimal? RequestedProcurementQuantity { get; set; }
        public decimal AllocatedToPoProcurementQuantity { get; set; }
        public decimal AcceptedProcurementQuantity { get; set; }
        public decimal ClosedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public int? RestockSourcingAllocationId { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
        public bool IsActiveReservation { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual PurchaseAdvice PurchaseAdvice { get; set; } = null!;
        public virtual RestockRequest RestockRequest { get; set; } = null!;
        public virtual Ingredient Ingredient { get; set; } = null!;
        public virtual Unit BaseUnit { get; set; } = null!;
        public virtual Unit? ProcurementUnit { get; set; }
        public virtual RestockSourcingAllocation? RestockSourcingAllocation { get; set; }
    }

    public class PurchaseAdviceTransition
    {
        public int PurchaseAdviceTransitionId { get; set; }
        public int PurchaseAdviceId { get; set; }
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }

        public virtual PurchaseAdvice PurchaseAdvice { get; set; } = null!;
        public virtual Staff ActorStaff { get; set; } = null!;
    }
}
