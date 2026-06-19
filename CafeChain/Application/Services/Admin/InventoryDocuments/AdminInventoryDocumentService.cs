using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using CafeChain.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentService : IAdminInventoryDocumentService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly IUserContext _userContext;

        private readonly IAdminInventoryDocumentExportService _exportService;

        public AdminInventoryDocumentService(IAdminInventoryDocumentRepository repository, IUserContext userContext, IAdminInventoryDocumentExportService exportService)
        {
            _repository = repository;
            _userContext = userContext;
            _exportService = exportService;
        }

        // =====================================================
        // INDEX
        // =====================================================

        public async Task<PaginatedListViewModel<AdminInventoryDocumentListVM>> GetPagedDocumentsAsync(AdminInventoryDocumentFilterDTO filter)
        {
            var query = _repository.GetDocumentsQuery();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(x => x.Code.Contains(filter.Search) || (x.PartnerName != null && x.PartnerName.Contains(filter.Search)));
            }

            if (filter.Type.HasValue)
            {
                query = query.Where(x => x.Type == filter.Type);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x => x.Status == filter.Status);
            }

            if (filter.Purpose.HasValue)
            {
                query = query.Where(x => x.Purpose == filter.Purpose);
            }

            if (filter.StoreId.HasValue)
            {
                query = query.Where(x => x.StoreId == filter.StoreId);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x => x.DocumentDate >= filter.FromDate);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x => x.DocumentDate <= filter.ToDate);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.DocumentDate)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x =>
                    new AdminInventoryDocumentListVM
                    {
                        InventoryDocumentId = x.InventoryDocumentId,

                        Code = x.Code,

                        Type = x.Type,

                        Status = x.Status,

                        Purpose = x.Purpose,

                        StoreName = x.Store.Name,

                        PartnerName = x.PartnerName,

                        DocumentDate = x.DocumentDate,

                        FinalAmount = x.FinalAmount,

                        ConfirmedAt = x.ConfirmedAt
                    })
                .ToListAsync();

            return new PaginatedListViewModel<AdminInventoryDocumentListVM>(items, totalCount, filter.Page, filter.PageSize);
        }

        public async Task<AdminInventoryDocumentIndexVM> GetIndexDataAsync(AdminInventoryDocumentFilterDTO filter)
        {
            var query = _repository.GetDocumentsQuery();

            // =====================================================
            // DASHBOARD
            // KHÔNG BỊ ẢNH HƯỞNG FILTER
            // =====================================================

            var totalDocuments = await query.CountAsync();

            var draftDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.DRAFT);

            var confirmedDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.CONFIRMED);

            var cancelledDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.CANCELLED);

            var thisMonthDocuments = await query.CountAsync(x => x.DocumentDate.Month == DateTime.Today.Month  && x.DocumentDate.Year == DateTime.Today.Year);

            // =====================================================
            // LIST
            // =====================================================

            var documents = await GetPagedDocumentsAsync(filter);

            // =====================================================
            // DROPDOWN
            // =====================================================

            var stores = await _repository.GetStoreDropdownAsync();

            return new AdminInventoryDocumentIndexVM
            {
                Filter = filter,

                Documents = documents,

                TotalDocuments = totalDocuments,

                DraftDocuments = draftDocuments,

                ConfirmedDocuments = confirmedDocuments,

                CancelledDocuments = cancelledDocuments,

                ThisMonthDocuments = thisMonthDocuments,

                Stores = stores
            };
        }

        // =====================================================
        // DETAIL
        // =====================================================

        public async Task<AdminInventoryDocumentDetailVM?> GetDetailAsync(int documentId)
        {
            var document = await _repository.GetDocumentWithDetailsAsync(documentId);

            if (document == null)
            {
                return null;
            }

            return new AdminInventoryDocumentDetailVM
            {
                InventoryDocumentId = document.InventoryDocumentId,

                Code = document.Code,

                Type = document.Type,

                Status = document.Status,

                Purpose = document.Purpose,

                DocumentDate = document.DocumentDate,

                RequestKey = document.RequestKey,

                IsProcessing = document.IsProcessing,

                StoreName = document.Store?.Name ?? "",

                StaffName = document.Staff?.FullName ?? "",

                ConfirmedAt = document.ConfirmedAt,

                PartnerType = document.PartnerType,

                PartnerName = document.PartnerName,

                SupplierName = document.Supplier?.Name,

                Note = document.Note,

                TotalAmount = document.TotalAmount,

                VatAmount = document.VatAmount,

                FinalAmount = document.FinalAmount,

                Details =
                    document.Details
                        .Select(x =>
                            new AdminInventoryDocumentDetailItemVM
                            {
                                IngredientName = x.Ingredient.Name,

                                UnitName = x.Unit.Name,

                                Quantity = x.Quantity,

                                BaseQuantity = x.BaseQuantity,

                                UnitPrice = x.UnitPrice,

                                CostPrice = x.CostPrice,

                                CostAmount = x.CostAmount,

                                TotalAmount = x.TotalAmount,

                                Note = x.Note
                            })
                        .ToList()
            };
        }

        // =====================================================
        // PREVIEW
        // =====================================================

        public async Task<AdminInventoryDocumentPreviewVM?> GetPreviewAsync(int documentId)
        {
            var snapshot = await _repository.GetSnapshotAsync(documentId);

            if (snapshot == null)
            {
                return null;
            }

            return new AdminInventoryDocumentPreviewVM
            {
                Code = snapshot.Code,

                DocumentDate = snapshot.DocumentDate,

                StoreName = snapshot.StoreName,

                StaffName = snapshot.StaffName,

                PartnerName = snapshot.PartnerName,

                TotalAmount = snapshot.TotalAmount,

                VatAmount = snapshot.VatAmount,

                FinalAmount = snapshot.FinalAmount,

                Details =
                    snapshot.Details
                        .Select(x =>
                            new AdminInventoryDocumentPreviewItemVM
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
        // CONFIRM
        // =====================================================

        public async Task<bool> ConfirmAsync(ConfirmInventoryDocumentDTO dto)
        {
            await _repository.BeginTransactionAsync();

            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(dto.InventoryDocumentId);

                if (document == null)
                {
                    return false;
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    return false;
                }

                await CreateSnapshotAsync(document);

                switch (document.Type)
                {
                    case InventoryDocumentType.IMPORT:
                        await ProcessImportAsync(document);
                        break;

                    case InventoryDocumentType.EXPORT:
                        await ProcessExportAsync(document);
                        break;

                    case InventoryDocumentType.WASTE:
                        await ProcessWasteAsync(document);
                        break;

                    case InventoryDocumentType.STOCK_TAKE:
                        await ProcessStockTakeAsync(document);
                        break;

                    case InventoryDocumentType.PRODUCTION_IN:
                        await ProcessProductionInAsync(document);
                        break;

                    case InventoryDocumentType.PRODUCTION_OUT:
                        await ProcessProductionOutAsync(document);
                        break;

                    case InventoryDocumentType.SALES_DEDUCTION:
                        await ProcessSalesDeductionAsync(document);
                        break;

                    default:
                        throw new InvalidOperationException($"Không hỗ trợ loại chứng từ {document.Type}");
                }

                document.Status = InventoryDocumentStatus.CONFIRMED;

                document.ConfirmedAt = DateTime.UtcNow;

                document.ConfirmedBy = dto.ConfirmedByStaffId;

                _repository.UpdateDocument(document);

                await CreateAuditLogAsync(document);

                await _repository.SaveChangesAsync();

                await _repository.CommitTransactionAsync();

                return true;
            }
            catch
            {
                await _repository.RollbackTransactionAsync();

                throw;
            }
        }

        // ====================================================
        // EXPORT FILE
        // ====================================================
        public async Task<byte[]?> ExportFileAsync(ExportInventoryDocumentDTO dto)
        {
            var document = await _repository.GetByIdAsync(dto.DocumentId);

            if (document == null)
            {
                return null;
            }

            var snapshot = await GetSnapshotAsync(dto.DocumentId);

            if (snapshot == null)
            {
                return null;
            }

            return dto.ExportType switch
            {
                InventoryDocumentExportType.PDF => await _exportService.ExportPdfAsync(snapshot),

                InventoryDocumentExportType.WORD => await _exportService.ExportWordAsync(snapshot),

                _ => null
            };
        }








        // =============================================================================================================================
        // =============================================================================================================================
        // ================================================= PRIVATE METHODS ===========================================================
        // =============================================================================================================================
        // =============================================================================================================================


        // =====================================================
        // SNAPSHOT
        // =====================================================

        private async Task CreateSnapshotAsync(InventoryDocument document)
        {
            if (await _repository.SnapshotExistsAsync(document.InventoryDocumentId))
            {
                return;
            }
            var snapshot =
                new InventoryDocumentSnapshot
                {
                    InventoryDocumentId = document.InventoryDocumentId,

                    Code = document.Code,

                    DocumentDate = document.DocumentDate,

                    StoreName = document.Store.Name,

                    StaffName = document.Staff.FullName,

                    PartnerName = document.PartnerName,

                    TotalAmount = document.TotalAmount ?? 0,

                    VatAmount = document.VatAmount ?? 0,

                    FinalAmount = document.FinalAmount ?? 0,

                    CreatedAt = DateTime.UtcNow
                };

            await _repository.AddSnapshotAsync(snapshot);

            await _repository.SaveChangesAsync();

            var details =
                document.Details
                .Select(x =>
                    new InventoryDocumentSnapshotDetail
                    {
                        InventoryDocumentSnapshotId = snapshot.InventoryDocumentSnapshotId,

                        ItemName = x.Ingredient.Name,

                        UnitName = x.Unit.Name,

                        Quantity = x.Quantity,

                        UnitPrice = x.UnitPrice ?? 0,

                        TotalAmount = x.TotalAmount ?? 0
                    });

            await _repository.AddSnapshotDetailsAsync(details);
        }

        // =====================================================
        // INVENTORY
        // =====================================================

        private async Task<StoreInventory> GetOrCreateInventoryAsync(int storeId, int ingredientId)
        {
            var inventory = await _repository.GetStoreInventoryAsync(storeId, ingredientId);

            if (inventory != null)
            {
                return inventory;
            }

            inventory =
                new StoreInventory
                {
                    StoreId = storeId,

                    IngredientId = ingredientId,

                    AvailableQty = 0,

                    ReservedQty = 0,

                    LastUpdated = DateTime.UtcNow
                };

            await _repository.AddStoreInventoryAsync(inventory);

            return inventory;
        }

        // =====================================================
        // FIFO
        // =====================================================

        private async Task<(decimal CostPrice, decimal CostAmount)> AllocateFifoAsync(InventoryDocumentDetail detail, int storeId)
        {
            decimal requiredQty = detail.BaseQuantity;

            decimal totalCost = 0;

            var allocations = new List<InventoryCostAllocation>();

            var layers = await _repository.GetAvailableCostLayersAsync(storeId, detail.IngredientId);

            foreach (var layer in layers)
            {
                if (requiredQty <= 0)
                {
                    break;
                }

                var consumeQty = Math.Min(requiredQty, layer.RemainingQuantity);

                allocations.Add(
                    new InventoryCostAllocation
                    {
                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        InventoryCostLayerId = layer.InventoryCostLayerId,

                        Quantity = consumeQty,

                        UnitCost = layer.UnitCost
                    });

                layer.RemainingQuantity -= consumeQty;

                _repository.UpdateCostLayer(layer);

                totalCost += consumeQty * layer.UnitCost;

                requiredQty -= consumeQty;
            }

            if (requiredQty > 0)
            {
                throw new InvalidOperationException($"Không đủ tồn FIFO cho nguyên liệu {detail.Ingredient.Name}");
            }

            await _repository.AddCostAllocationsAsync(allocations);

            var avgCost = totalCost / detail.BaseQuantity;

            return (avgCost, totalCost);
        }

        // =====================================================
        // IMPORT
        // =====================================================

        private async Task ProcessImportAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                detail.CostPrice = detail.UnitPrice ?? 0;
                detail.CostAmount = detail.BaseQuantity * (detail.UnitPrice ?? 0);
                _repository.UpdateDocumentDetail(detail);

                var inventory = await _repository.GetStoreInventoryAsync(document.StoreId, detail.IngredientId);

                if (inventory == null)
                {
                    inventory =
                        new StoreInventory
                        {
                            StoreId = document.StoreId,

                            IngredientId = detail.IngredientId,

                            AvailableQty = detail.BaseQuantity,

                            ReservedQty = 0,

                            LastUpdated = DateTime.UtcNow
                        };

                    await _repository.AddStoreInventoryAsync(inventory);
                }
                else
                {
                    inventory.AvailableQty += detail.BaseQuantity;

                    _repository.UpdateStoreInventory(inventory);
                }

                await _repository
                    .AddStoreInventorySnapshotAsync(
                        new StoreInventorySnapshot
                        {
                            StoreId = document.StoreId,

                            IngredientId = detail.IngredientId,

                            Quantity = inventory.AvailableQty,

                            AvgCost = detail.UnitPrice ?? 0,

                            UpdatedAt = DateTime.UtcNow
                        });

                await _repository
                    .AddCostLayerAsync(
                        new InventoryCostLayer
                        {
                            StoreId = document.StoreId,

                            IngredientId = detail.IngredientId,

                            Quantity = detail.BaseQuantity,

                            RemainingQuantity = detail.BaseQuantity,

                            UnitCost = detail.UnitPrice ?? 0,

                            CreatedAt = DateTime.UtcNow
                        });

                await _repository
                    .AddInventoryTransactionAsync(
                        new InventoryTransaction
                        {
                            StoreInventoryId = inventory.StoreInventoryId,

                            InventoryDocumentId = document.InventoryDocumentId,

                            Type = InventoryDocumentType.IMPORT,

                            Quantity = detail.BaseQuantity,

                            BeforeQty = inventory.AvailableQty - detail.BaseQuantity,

                            AfterQty = inventory.AvailableQty,

                            UnitCost = detail.UnitPrice,

                            TotalCost = detail.BaseQuantity * (detail.UnitPrice ?? 0),

                            CreatedAt = DateTime.UtcNow
                        });
            }

            if (document.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE)
            {
                await _repository.AddDebtAsync(
                    new InventoryDebt
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        PartnerType = InventoryPartnerType.SUPPLIER,

                        PartnerId = document.SupplierId,

                        PartnerName = document.Supplier?.Name ?? "",

                        Amount = document.FinalAmount ?? 0,

                        PaidAmount = 0,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // EXPORT
        // =====================================================

        private async Task ProcessExportAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                if (inventory.AvailableQty < detail.BaseQuantity)
                {
                    throw new InvalidOperationException($"Không đủ tồn kho: {detail.Ingredient.Name}");
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryDocumentType.EXPORT,

                        Quantity = -detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }

            if (document.Purpose == InventoryDocumentPurpose.DEBT)
            {
                await _repository.AddDebtAsync(
                    new InventoryDebt
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        PartnerType = document.PartnerType,

                        PartnerId = document.PartnerId,

                        PartnerName = document.PartnerName ?? "",

                        Amount = document.FinalAmount ?? 0,

                        PaidAmount = 0,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // WASTE
        // =====================================================

        private async Task ProcessWasteAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                if (inventory.AvailableQty < detail.BaseQuantity)
                {
                    throw new InvalidOperationException($"Không đủ tồn kho: {detail.Ingredient.Name}");
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryDocumentType.WASTE,

                        Quantity = -detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // SALES DEDUCTION
        // =====================================================
        private async Task ProcessSalesDeductionAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId =
                            document.InventoryDocumentId,

                        Type = InventoryDocumentType.SALES_DEDUCTION,

                        Quantity = -detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // ====================================================
        // PRODUCTION OUT
        // ====================================================
        private async Task ProcessProductionOutAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryDocumentType.PRODUCTION_OUT,

                        Quantity = -detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // PRODUCTION IN
        // =====================================================
        private async Task ProcessProductionInAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty += detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await _repository.AddCostLayerAsync(
                    new InventoryCostLayer
                    {
                        StoreId = document.StoreId,

                        IngredientId = detail.IngredientId,

                        Quantity = detail.BaseQuantity,

                        RemainingQuantity = detail.BaseQuantity,

                        UnitCost = detail.CostPrice ?? 0,

                        CreatedAt = DateTime.UtcNow
                    });

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryDocumentType.PRODUCTION_IN,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = detail.CostPrice,

                        TotalCost = detail.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }


        // =====================================================
        // STOCK TAKE
        // =====================================================

        private async Task ProcessStockTakeAsync(InventoryDocument document)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var systemQty = inventory.AvailableQty;

                var actualQty = detail.BaseQuantity;

                var variance = actualQty - systemQty;

                if (variance == 0)
                {
                    continue;
                }

                inventory.AvailableQty += variance;

                _repository.UpdateStoreInventory(inventory);

                await _repository
                    .AddInventoryTransactionAsync(
                        new InventoryTransaction
                        {
                            StoreInventoryId = inventory.StoreInventoryId,

                            InventoryDocumentId = document.InventoryDocumentId,

                            Quantity = variance,

                            BeforeQty = systemQty,

                            AfterQty = inventory.AvailableQty,

                            CreatedAt = DateTime.UtcNow
                        });
            }
        }

        // =====================================================
        // AUDIT
        // =====================================================

        private async Task CreateAuditLogAsync(InventoryDocument document)
        {
            var log =
                new AuditLog
                {
                    TableName = nameof(InventoryDocument),

                    RecordId = document.InventoryDocumentId,

                    Action = "CONFIRM",

                    NewData = JsonSerializer.Serialize(document),

                    UserId = _userContext.StaffId,

                    CreatedAt = DateTime.UtcNow
                };

            await _repository.AddAuditLogAsync(log);
        }
    }
}