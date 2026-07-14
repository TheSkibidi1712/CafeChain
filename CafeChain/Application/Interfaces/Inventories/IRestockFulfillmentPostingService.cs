using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public class RegisterRestockFulfillmentPostingCommand
    {
        public int RestockRequestId { get; set; }
        public int DestinationStoreId { get; set; }
        public string SourceDocumentType { get; set; } = string.Empty;
        public int SourceDocumentId { get; set; }
        public int SourceDocumentLineId { get; set; }
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public decimal Quantity { get; set; }
        public int BaseUnitId { get; set; }
        public int ActorStaffId { get; set; }
        public string? Reason { get; set; }
    }

    public class RestockFulfillmentPostingResult
    {
        public bool WasReplay { get; set; }
        public decimal FulfilledQuantity { get; set; }
        public decimal TargetQuantity { get; set; }
        public string RequestStatus { get; set; } = string.Empty;
    }

    public interface IRestockFulfillmentPostingService
    {
        Task<ServiceResult<RestockFulfillmentPostingResult>> RegisterAsync(
            RegisterRestockFulfillmentPostingCommand command);
    }
}
