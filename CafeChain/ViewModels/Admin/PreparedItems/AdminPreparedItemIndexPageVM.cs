using System.Collections.Generic;
using CafeChain.Application.DTOs.Admin.PreparedItems;

namespace CafeChain.ViewModels.Admin.PreparedItems
{
    public class AdminPreparedItemIndexPageVM
    {
        public List<AdminPreparedItemDTO> Items { get; set; } = new();
        public string? Search { get; set; }
        public bool? Status { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public bool CanWrite { get; set; }
    }
}
