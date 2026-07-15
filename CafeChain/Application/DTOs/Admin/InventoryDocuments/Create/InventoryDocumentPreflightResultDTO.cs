using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;

public sealed class InventoryDocumentPreflightResultDTO
{
    public InventoryIssueOutcome Outcome { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public List<InventoryDocumentPreflightLineDTO> Lines { get; set; } = [];
}

public sealed class InventoryDocumentPreflightLineDTO
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal BeforeQty { get; set; }
    public decimal IssueQty { get; set; }
    public decimal ProjectedAfterQty { get; set; }
    public decimal EffectiveMaxNegativeQty { get; set; }
    public InventoryIssueOutcome Outcome { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}
