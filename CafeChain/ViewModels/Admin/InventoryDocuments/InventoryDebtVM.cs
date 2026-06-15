namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDebtVM
    {
        public int DocumentId { get; set; }

        public string PartnerName { get; set; }

        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }

        public bool IsPaid => PaidAmount >= Amount;
    }
}
