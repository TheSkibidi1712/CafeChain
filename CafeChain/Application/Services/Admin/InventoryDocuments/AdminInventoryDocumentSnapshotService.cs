using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Enums.Inventory;
namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentSnapshotService : IAdminInventoryDocumentSnapshotService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        public AdminInventoryDocumentSnapshotService(IAdminInventoryDocumentRepository repository)
        {
            _repository = repository;
        }

        // ====================================================
        // SNAPSHOT
        // ====================================================
        public async Task<InventoryDocumentSnapshotDTO?> GetSnapshotAsync(int documentId)
        {
            var snapshot = await _repository.GetSnapshotAsync(documentId);

            if (snapshot == null)
            {
                return null;
            }

            return new InventoryDocumentSnapshotDTO
            {
                SnapshotId = snapshot.InventoryDocumentSnapshotId,

                InventoryDocumentId = snapshot.InventoryDocumentId,

                Type = snapshot.Type,

                Purpose = snapshot.Purpose,

                Status = snapshot.Status,

                CostComplete = snapshot.CostComplete,

                Code = snapshot.Code,

                DocumentDate = snapshot.DocumentDate,

                StoreName = snapshot.StoreName,

                StaffName = snapshot.StaffName,

                PartnerName = snapshot.PartnerName,

                TotalAmount = snapshot.TotalAmount,

                VatAmount = snapshot.VatAmount,

                FinalAmount = snapshot.FinalAmount,

                CreatedAt = snapshot.CreatedAt,

                Details =
                    snapshot.Details
                    .Select(x =>
                        new InventoryDocumentSnapshotItemDTO
                        {
                            ItemName = x.ItemName,

                            UnitName = x.UnitName,

                            Quantity = x.Quantity,

                            UnitPrice = x.UnitPrice,

                            TotalAmount = x.TotalAmount
                        })
                    .ToList()
            };
        }

        // =====================================================
        // SNAPSHOT
        // =====================================================

        public async Task CreateSnapshotAsync(InventoryDocument document)
        {
            if (document.Status != InventoryDocumentStatus.CONFIRMED)
                throw new InvalidOperationException("Chỉ chứng từ đã xác nhận mới được tạo snapshot.");

            if (await _repository.SnapshotExistsAsync(document.InventoryDocumentId))
            {
                return;
            }
            var snapshot =
                new InventoryDocumentSnapshot
                {
                    InventoryDocumentId = document.InventoryDocumentId,

                    InventoryDocument = document,

                    Type = document.Type,

                    Purpose = document.Purpose,

                    Status = document.Status,

                    NegativeApprovalId = document.NegativeApproval?.InventoryNegativeApprovalId,

                    PolicyVersion = document.NegativeApproval?.PolicyVersion,

                    EffectiveMaxNegativeQty = document.NegativeApproval?.Lines.Any() == true
                        ? document.NegativeApproval.Lines.Max(x => x.EffectiveMaxNegativeQty)
                        : null,

                    BeforeQty = document.NegativeApproval?.Lines.Any() == true
                        ? document.NegativeApproval.Lines.Sum(x => x.BeforeQty)
                        : null,

                    AfterQty = document.NegativeApproval?.Lines.Any() == true
                        ? document.NegativeApproval.Lines.Sum(x => x.ProjectedAfterQty)
                        : null,

                    Code = document.Code ?? string.Empty,

                    DocumentDate = document.DocumentDate,

                    StoreName = document.Store?.Name ?? string.Empty,

                    StaffName = document.Staff?.FullName ?? string.Empty,

                    PartnerName = document.PartnerName,

                    TotalAmount = document.TotalAmount ?? 0,

                    VatAmount = document.VatAmount ?? 0,

                    FinalAmount = document.FinalAmount ?? 0,

                    CostComplete = document.Details.All(x =>
                        x.CostPrice.HasValue && x.CostAmount.HasValue),

                    CreatedAt = DateTime.UtcNow,

                    Details = document.Details
                        .Select(x => new InventoryDocumentSnapshotDetail
                        {
                            ItemName = x.Ingredient?.Name ?? string.Empty,
                            UnitName = x.Unit?.Name ?? string.Empty,
                            Quantity = x.Quantity,
                            UnitPrice = x.UnitPrice ?? 0,
                            TotalAmount = x.TotalAmount ?? 0
                        })
                        .ToList()
                };

            await _repository.AddSnapshotAsync(snapshot);
        }
    }
}
