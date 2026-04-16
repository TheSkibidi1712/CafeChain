using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Inventories
{
    public class InventoryDebt
    {
        public int InventoryDebtId { get; set; }

        public int InventoryDocumentId { get; set; } // phiếu xuất

        public InventoryPartnerType PartnerType { get; set; }
        public int? PartnerId { get; set; }
        public string PartnerName { get; set; }

        public decimal Amount { get; set; } // tổng tiền nợ
        public decimal PaidAmount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public bool IsPaid => PaidAmount >= Amount;

        public virtual InventoryDocument InventoryDocument { get; set; }
    }
}
