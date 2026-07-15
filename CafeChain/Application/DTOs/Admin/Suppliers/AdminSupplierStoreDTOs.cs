using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    public class AdminSupplierStoreDTO
    {
        public int SupplierStoreId { get; set; }
        public int SupplierId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = "";
        public bool Active { get; set; }
        public int? LeadTimeOverrideDays { get; set; }
        public string? DeliverySchedule { get; set; }
        public string? Note { get; set; }
        public string RowVersion { get; set; } = "";
    }

    public class AdminSupplierStoreSaveDTO
    {
        public int? SupplierStoreId { get; set; }
        [Required] public int SupplierId { get; set; }
        [Required] public int StoreId { get; set; }
        [Range(0, int.MaxValue)] public int? LeadTimeOverrideDays { get; set; }
        [MaxLength(300)] public string? DeliverySchedule { get; set; }
        [MaxLength(1000)] public string? Note { get; set; }
        public bool Active { get; set; } = true;
        public string? RowVersion { get; set; }
    }
}
