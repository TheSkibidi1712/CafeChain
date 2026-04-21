    using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
    using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
    using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
    using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
    using CafeChain.Models.Enums.Inventory;
    using CafeChain.Models.Inventories;
    using CafeChain.Models.Stores;
    using CafeChain.ViewModels.Admin.InventoryDocuments;
    namespace CafeChain.Application.Services.Admin.InventoryTransfers
    {
        public class AdminInventoryTransferService : IAdminInventoryTransferService
        {
            private readonly IAdminInventoryTransferRepository _repository;
            private readonly IAdminInventoryDocumentRepository _documentRepository;
            private readonly IUserContext _userContext;
            public AdminInventoryTransferService(IAdminInventoryTransferRepository repository, IAdminInventoryDocumentRepository documentRepository, IUserContext userContext)
            {
                _repository = repository;
                _documentRepository = documentRepository;
                _userContext = userContext;
            }
            public async Task CreateInternalTransferAsync(InventoryTransferCreateVM vm)
            {
                if (vm.FromStoreId == vm.ToStoreId)
                    throw new Exception("Không thể chuyển cùng 1 kho");

                if (vm.Items == null || !vm.Items.Any())
                    throw new Exception("Không có dữ liệu chuyển kho");

                var staffId = _userContext.StaffId;

                var hasPermission = await _documentRepository
                    .CheckStaffHasStoreAsync(staffId, vm.FromStoreId);

                if (!hasPermission)
                    throw new Exception("Bạn không có quyền thao tác kho nguồn");

                var stores = await _repository.GetStoresByIdsAsync(
                    new List<int> { vm.FromStoreId, vm.ToStoreId });

                var fromStore = stores.FirstOrDefault(x => x.StoreId == vm.FromStoreId)
                    ?? throw new Exception("Kho nguồn không tồn tại");

                var toStore = stores.FirstOrDefault(x => x.StoreId == vm.ToStoreId)
                    ?? throw new Exception("Kho đích không tồn tại");

                using var tran = await _documentRepository.BeginTransactionAsync();

                try
                {
                    // convert về base unit
                    var items = await ConvertTransferItems(vm.Items);

                    // check tồn kho kho xuất
                    await ValidateStock(
                        vm.FromStoreId,
                        items.Select(x => (
                            new InventoryDocumentDetailCreateVM
                            {
                                IngredientId = x.item.IngredientId,
                                Quantity = x.item.Quantity,
                                UnitId = x.item.UnitId,
                                UnitPrice = x.item.UnitPrice,
                                Note = x.item.Note
                            },
                            x.baseQty
                        )).ToList()
                    );

                    // =====================================================
                    // 1. TẠO PHIẾU XUẤT KHO (CONFIRMED)
                    // =====================================================

                    var exportDoc = CreateDocument(
                        new InventoryDocumentVM
                        {
                            StoreId = vm.FromStoreId,
                            Type = InventoryDocumentType.EXPORT,
                            Purpose = InventoryDocumentPurpose.INTERNAL_OUT,
                            PartnerType = InventoryPartnerType.STORE,
                            PartnerId = vm.ToStoreId,
                            PartnerName = toStore.Name,
                            Note = vm.Note
                        },
                        staffId,
                        items.Select(x => (
                            new InventoryDocumentDetailCreateVM
                            {
                                IngredientId = x.item.IngredientId,
                                Quantity = x.item.Quantity,
                                UnitId = x.item.UnitId,
                                UnitPrice = x.item.UnitPrice,
                                Note = x.item.Note
                            },
                            x.baseQty
                        )).ToList()
                    );

                    exportDoc.Status = InventoryDocumentStatus.CONFIRMED;

                    await _documentRepository.AddAsync(exportDoc);
                    await _documentRepository.SaveChangesAsync();

                    // trừ tồn kho ngay
                    await UpdateStockAndTransaction(
                        new InventoryDocumentVM
                        {
                            StoreId = vm.FromStoreId,
                            Type = InventoryDocumentType.EXPORT
                        },
                        exportDoc,
                        items.Select(x => (
                            new InventoryDocumentDetailCreateVM
                            {
                                IngredientId = x.item.IngredientId,
                                Quantity = x.item.Quantity,
                                UnitId = x.item.UnitId
                            },
                            x.baseQty
                        )).ToList()
                    );

                    // =====================================================
                    // 2. TẠO PHIẾU NHẬP KHO (DRAFT)
                    // chưa cộng tồn kho
                    // =====================================================

                    var importDoc = CreateDocument(
                        new InventoryDocumentVM
                        {
                            StoreId = vm.ToStoreId,
                            Type = InventoryDocumentType.IMPORT,
                            Purpose = InventoryDocumentPurpose.IMPORT_INTERNAL,
                            PartnerType = InventoryPartnerType.STORE,
                            PartnerId = vm.FromStoreId,
                            PartnerName = fromStore.Name,
                            Note = vm.Note
                        },
                        staffId,
                        items.Select(x => (
                            new InventoryDocumentDetailCreateVM
                            {
                                IngredientId = x.item.IngredientId,
                                Quantity = x.item.Quantity,
                                UnitId = x.item.UnitId,
                                UnitPrice = x.item.UnitPrice,
                                Note = x.item.Note
                            },
                            x.baseQty
                        )).ToList()
                    );

                    importDoc.Status = InventoryDocumentStatus.DRAFT;

                    await _documentRepository.AddAsync(importDoc);
                    await _documentRepository.SaveChangesAsync();

                    // =====================================================
                    // 3. TẠO TRANSFER
                    // =====================================================

                    var transfer = new InventoryTransfer
                    {
                        FromStoreId = vm.FromStoreId,
                        ToStoreId = vm.ToStoreId,

                        ExportDocumentId = exportDoc.InventoryDocumentId,
                        ImportDocumentId = importDoc.InventoryDocumentId,

                        Status = InventoryTransferStatus.PENDING,

                        TotalExportQty = items.Sum(x => x.baseQty),
                        TotalReceivedQty = 0,

                        CreatedAt = DateTime.Now,

                        Details = items.Select(x => new InventoryTransferDetail
                        {
                            IngredientId = x.item.IngredientId,
                            ExportQuantity = x.baseQty,
                            ReceivedQuantity = 0,
                            UnitPrice = x.item.UnitPrice,
                            Note = x.item.Note
                        }).ToList()
                    };

                    await _repository.AddTransferAsync(transfer);

                    await _documentRepository.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                catch
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }

            public async Task ConfirmTransferReceiveAsync(int transferId)
            {
                var transfer = await _repository.GetTransferByIdAsync(transferId)
                    ?? throw new Exception("Transfer không tồn tại");

                if (transfer.Status != InventoryTransferStatus.READY)
                    throw new Exception("Transfer chưa nhận đủ");

                if (transfer.ImportDocumentId == null)
                        throw new Exception("Transfer chưa có phiếu nhập");

                if (!transfer.Details.Any(x => x.ReceivedQuantity > 0))
                    throw new Exception("Chưa có số lượng nhận");

            using var tran = await _documentRepository.BeginTransactionAsync();

                try
                {
                    var importDoc = await _repository
                        .GetDocumentWithDetailsAsync(transfer.ImportDocumentId.Value);

                    foreach (var detail in transfer.Details)
                    {
                        if (detail.ReceivedQuantity <= 0)
                            continue;

                        var stock = await _documentRepository.GetStoreInventoryAsync(
                            transfer.ToStoreId,
                            detail.IngredientId
                        );

                        if (stock == null)
                        {
                            stock = new StoreInventory
                            {
                                StoreId = transfer.ToStoreId,
                                IngredientId = detail.IngredientId,
                                AvailableQty = 0
                            };

                            await _documentRepository.AddStoreInventoryAsync(stock);
                        }

                        var before = stock.AvailableQty;

                        stock.AvailableQty += detail.ReceivedQuantity;

                        var after = stock.AvailableQty;

                        await _documentRepository.AddTransactionAsync(new InventoryTransaction
                        {
                            StoreInventory = stock,
                            Type = InventoryDocumentType.IMPORT,
                            Quantity = detail.ReceivedQuantity,
                            BeforeQty = before,
                            AfterQty = after,
                            InventoryDocumentId = transfer.ImportDocumentId,
                            CreatedAt = DateTime.Now
                        });
                    }

                    importDoc.Status = InventoryDocumentStatus.CONFIRMED;
                    transfer.Status = InventoryTransferStatus.COMPLETED;

                    await _documentRepository.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                catch
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }

            public async Task ReceiveTransferAsync(int transferId, List<InventoryTransferReceiveItemVM> receivedItems)
            {
                var transfer = await _repository.GetTransferByIdAsync(transferId)
                    ?? throw new Exception("Transfer không tồn tại");

                if (receivedItems == null || !receivedItems.Any())
                    throw new Exception("Không có dữ liệu nhận");

                if (transfer.ImportDocumentId == null)
                        throw new Exception("Transfer chưa có phiếu nhập");

                using var tran = await _documentRepository.BeginTransactionAsync();

                try
                {
                    var importDoc = await _repository
                        .GetDocumentWithDetailsAsync(transfer.ImportDocumentId.Value);

                    foreach (var item in receivedItems)
                    {
                        var transferDetail = transfer.Details
                            .FirstOrDefault(x => x.IngredientId == item.IngredientId)
                            ?? throw new Exception("Không tìm thấy nguyên liệu");

                        var importDetail = importDoc.Details
                            .FirstOrDefault(x => x.IngredientId == item.IngredientId)
                            ?? throw new Exception("Không tìm thấy detail phiếu nhập");

                        var remain = transferDetail.ExportQuantity
                                     - transferDetail.ReceivedQuantity;

                        if (item.BaseQuantity <= 0)
                            throw new Exception("Số lượng nhận không hợp lệ");

                        if (item.BaseQuantity > remain)
                            throw new Exception("Số lượng nhận vượt số lượng chuyển");

                        transferDetail.ReceivedQuantity += item.BaseQuantity;

                        importDetail.BaseQuantity = transferDetail.ReceivedQuantity;
                        importDetail.Quantity = transferDetail.ReceivedQuantity;
                    }

                    transfer.TotalReceivedQty = transfer.Details.Sum(x => x.ReceivedQuantity);

                transfer.Status = transfer.TotalReceivedQty == 0 ? InventoryTransferStatus.PENDING : transfer.TotalReceivedQty < transfer.TotalExportQty ? InventoryTransferStatus.IN_PROGRESS : InventoryTransferStatus.READY;

                await _documentRepository.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                catch
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }

        public async Task ConfirmAllAsync(int transferId)
        {
            var transfer = await _repository.GetTransferByIdAsync(transferId)
                ?? throw new Exception("Không tìm thấy transfer");

            if (transfer.ImportDocumentId == null)
                throw new Exception("Transfer chưa có phiếu nhập");

            using var tran = await _documentRepository.BeginTransactionAsync();

            try
            {
                var importDoc = await _repository
                    .GetDocumentWithDetailsAsync(transfer.ImportDocumentId.Value);

                foreach (var d in transfer.Details)
                {
                    var remaining = d.ExportQuantity - d.ReceivedQuantity;

                    if (remaining <= 0) continue;

                    d.ReceivedQuantity += remaining;

                    var stock = await _documentRepository.GetStoreInventoryAsync(
                        transfer.ToStoreId,
                        d.IngredientId
                    );

                    if (stock == null)
                    {
                        stock = new StoreInventory
                        {
                            StoreId = transfer.ToStoreId,
                            IngredientId = d.IngredientId,
                            AvailableQty = 0
                        };

                        await _documentRepository.AddStoreInventoryAsync(stock);
                    }

                    var before = stock.AvailableQty;
                    stock.AvailableQty += remaining;

                    await _documentRepository.AddTransactionAsync(new InventoryTransaction
                    {
                        StoreInventory = stock,
                        Type = InventoryDocumentType.IMPORT,
                        Quantity = remaining,
                        BeforeQty = before,
                        AfterQty = stock.AvailableQty,
                        InventoryDocumentId = transfer.ImportDocumentId,
                        CreatedAt = DateTime.Now
                    });

                    // update import doc detail
                    var importDetail = importDoc.Details
                        .First(x => x.IngredientId == d.IngredientId);

                    importDetail.BaseQuantity = d.ReceivedQuantity;
                    importDetail.Quantity = d.ReceivedQuantity;
                }

                transfer.TotalReceivedQty = transfer.Details.Sum(x => x.ReceivedQuantity);
                transfer.Status = InventoryTransferStatus.COMPLETED;
                importDoc.Status = InventoryDocumentStatus.CONFIRMED;

                await _documentRepository.SaveChangesAsync();
                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        public async Task CancelTransferAsync(int transferId)
            {
                var transfer = await _repository.GetTransferByIdAsync(transferId)
                    ?? throw new Exception("Không tìm thấy transfer");

                if (transfer.Status == InventoryTransferStatus.COMPLETED)
                    throw new Exception("Không thể hủy transfer đã hoàn thành");

                transfer.Status = InventoryTransferStatus.CANCELLED;

                await _documentRepository.SaveChangesAsync();
            }

            public async Task<List<InventoryTransfer>> GetPendingTransfersToStore(int storeId)
            {
                return await _repository.GetPendingTransfersToStoreAsync(storeId);
            }

            public async Task<InventoryTransfer?> GetTransferByIdAsync(int id)
            {
                return await _repository.GetTransferByIdAsync(id);
            }


            // ================= PRIVATE HELPERS =================
            // CẬP NHẬT TỒN KHO VÀ GHI NHẬT KÝ GIAO DỊCH
            private async Task UpdateStockAndTransaction(InventoryDocumentVM vm, InventoryDocument document, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
            {
                foreach (var (item, baseQty) in items)
                {
                    var stock = await _documentRepository.GetStoreInventoryAsync(vm.StoreId, item.IngredientId);

                    if (stock == null)
                    {
                        stock = new StoreInventory
                        {
                            StoreId = vm.StoreId,
                            IngredientId = item.IngredientId,
                            AvailableQty = 0,
                            ReservedQty = 0,
                            LastUpdated = DateTime.Now
                        };

                        await _documentRepository.AddStoreInventoryAsync(stock);
                    }

                    var beforeQty = stock.AvailableQty;

                    switch (vm.Type)
                    {
                        case InventoryDocumentType.IMPORT:
                            stock.AvailableQty += baseQty;
                            break;

                        case InventoryDocumentType.EXPORT:
                        case InventoryDocumentType.WASTE:
                            stock.AvailableQty -= baseQty;
                            break;
                    }

                    var afterQty = stock.AvailableQty;
                    stock.LastUpdated = DateTime.Now;

                    var transaction = new InventoryTransaction
                    {
                        StoreInventory = stock,
                        Type = vm.Type,
                        Quantity = vm.Type == InventoryDocumentType.IMPORT ? baseQty : -baseQty,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        InventoryDocument = document,
                        CreatedAt = DateTime.Now
                    };

                    await _documentRepository.AddTransactionAsync(transaction);
                }
            }

            // KIỂM TRA TỒN KHO TRƯỚC KHI XUẤT KHO/HỦY HÀNG
            private async Task ValidateStock(int storeId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
            {
                var grouped = items
                    .GroupBy(x => x.item.IngredientId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.baseQty));

                var stocks = await _documentRepository.GetStoreInventoriesAsync(storeId);

                foreach (var kv in grouped)
                {
                    var stock = stocks.FirstOrDefault(x => x.IngredientId == kv.Key);
                    var available = stock?.AvailableQty ?? 0;

                    if (available < kv.Value)
                    {
                        throw new Exception(
                            $"Không đủ tồn kho (IngredientId={kv.Key}) - Tồn: {available}, Cần: {kv.Value}"
                        );
                    }
                }
            }

            // CHUYỂN ĐỔI SỐ LƯỢNG VỀ ĐƠN VỊ CƠ SỞ CHO TRANSFER
            private async Task<List<(InventoryTransferItemVM item, decimal baseQty)>> ConvertTransferItems(List<InventoryTransferItemVM> items)
            {
                var ingredientDict = new Dictionary<int, Ingredient>();
                var result = new List<(InventoryTransferItemVM, decimal)>();

                foreach (var item in items)
                {
                    if (!ingredientDict.ContainsKey(item.IngredientId))
                    {
                        var ing = await _documentRepository.GetIngredientAsync(item.IngredientId)
                            ?? throw new Exception($"Không tìm thấy nguyên liệu {item.IngredientId}");

                        ingredientDict[item.IngredientId] = ing;
                    }

                    var ingre = ingredientDict[item.IngredientId];

                    var baseQty = await ConvertToBaseUnit(
                        new InventoryDocumentDetailCreateVM
                        {
                            IngredientId = item.IngredientId,
                            Quantity = item.Quantity,
                            UnitId = item.UnitId
                        },
                        ingre.BaseUnitId
                    );

                    result.Add((item, baseQty));
                }

                return result;
            }

            // Tạo document từ VM
            private InventoryDocument CreateDocument(InventoryDocumentVM vm, int staffId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
            {
                var document = new InventoryDocument
                {
                    Code = GenerateCode(vm.Type),
                    StoreId = vm.StoreId,
                    StaffId = staffId,
                    SupplierId = vm.SupplierId,
                    DocumentDate = DateTime.SpecifyKind(vm.DocumentDate, DateTimeKind.Local),
                    Type = vm.Type,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    Purpose = vm.Purpose,
                    PartnerType = vm.PartnerType,
                    PartnerId = vm.PartnerId,
                    PartnerName = vm.PartnerName,
                    Note = vm.Note,
                    Details = new List<InventoryDocumentDetail>()
                };

                foreach (var (item, baseQty) in items)
                {
                    var unitPrice = vm.Type == InventoryDocumentType.IMPORT
                        || vm.Purpose == InventoryDocumentPurpose.DEBT
                            ? item.UnitPrice
                            : null;

                    var totalAmount = unitPrice.HasValue
                        ? unitPrice.Value * item.Quantity
                        : (decimal?)null;

                    document.Details.Add(new InventoryDocumentDetail
                    {
                        IngredientId = item.IngredientId,
                        UnitId = item.UnitId,
                        Quantity = item.Quantity,
                        BaseQuantity = baseQty,
                        UnitPrice = unitPrice,
                        TotalAmount = totalAmount,
                        Note = item.Note
                    });
                }

                return document;
            }

            private async Task<decimal> ConvertToBaseUnit(InventoryDocumentDetailCreateVM item, int baseUnitId)
            {
                if (item.UnitId == baseUnitId)
                    return item.Quantity;

                var conversion = await _documentRepository.GetConversionAsync(
                    item.IngredientId,
                    item.UnitId,
                    baseUnitId
                );

                if (conversion == null)
                    throw new Exception($"Không có quy đổi đơn vị cho nguyên liệu {item.IngredientId}");

                return item.Quantity * (conversion.ToQuantity / conversion.FromQuantity);
            }

            private string GenerateCode(InventoryDocumentType type)
            {
                var prefix = type switch
                {
                    InventoryDocumentType.IMPORT => "NK",
                    InventoryDocumentType.EXPORT => "XK",
                    InventoryDocumentType.WASTE => "HK",
                    InventoryDocumentType.STOCK_TAKE => "KK"
                };

                return $"{prefix}-{DateTime.Now:yyyyMMddHHmmssfff}";
            }
        }
    }
