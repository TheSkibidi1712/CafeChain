using System.Collections.Generic;
using CafeChain.Application.DTOs.Admin.Inventory;
using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Inventories
{
    public class AdminInventoryListViewModel
    {
        // Summary Cards Data
        public int TotalItemsCount { get; set; }
        public double TotalItemsGrowthPercentage { get; set; } // e.g. +2.4%

        public int LowStockCount { get; set; }
        
        public decimal TotalInventoryValue { get; set; }

        public int MonthlyImportBatches { get; set; }

        // Filtering & Search
        public string SearchTerm { get; set; }
        public string SelectedType { get; set; }
        public string SelectedStatus { get; set; }

        // Options for dropdowns
        public List<string> Types { get; set; }
        public List<string> Statuses { get; set; }

        // Paginated List
        public PaginatedListViewModel<AdminIngredientDto> IngredientsList { get; set; }
    }
}
