using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.Production
{
    public sealed class CreateAndConfirmProductionRunRequest
    {
        /// <summary>Client crypto.randomUUID() — required.</summary>
        public Guid? RequestKey { get; set; }

        /// <summary>Optional; defaults to staff home store. Multi-store only when authorized.</summary>
        public int? StoreId { get; set; }

        public int RecipeId { get; set; }

        public decimal RequestedRunCount { get; set; }

        public string? Notes { get; set; }
    }

    public sealed class ProductionRunResultDto
    {
        public int ProductionRunId { get; set; }
        public int StoreId { get; set; }
        public int RecipeId { get; set; }
        public decimal RequestedRunCount { get; set; }
        public string Status { get; set; } = nameof(ProductionRunStatus.Confirmed).ToUpperInvariant();
        public DateTime ConfirmedAt { get; set; }
        public bool WasReplay { get; set; }
        public bool StockApplied { get; set; }
        public string MessageKey { get; set; } = string.Empty;
        public string? RecipeName { get; set; }
    }

    public sealed class ProductionRunHistoryItemDto
    {
        public int ProductionRunId { get; set; }
        public int StoreId { get; set; }
        public string? StoreName { get; set; }
        public string? RecipeName { get; set; }
        public int RecipeId { get; set; }
        public decimal RequestedRunCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int CreatedByStaffId { get; set; }
        public string? ActorName { get; set; }
        public bool StockApplied { get; set; }
        public bool CanApplyStock { get; set; }
    }
}
