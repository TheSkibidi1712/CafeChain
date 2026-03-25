using System.Collections.Generic;

namespace CafeChain.Application.DTOs.Admin.Inventory
{
    public class AdminCreateStockImportDto
    {
        public string Note { get; set; }
        public List<AdminStockImportDetailDto> Details { get; set; } = new List<AdminStockImportDetailDto>();
    }
}
