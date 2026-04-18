using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
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

            var staffId = _userContext.StaffId;

            var hasFrom = await _documentRepository.CheckStaffHasStoreAsync(staffId, vm.FromStoreId);
            if (!hasFrom)
                throw new Exception("Không có quyền thao tác kho");

            // 🔥 LẤY STORE
            var stores = await _repository.GetStoresByIdsAsync(
                new List<int> { vm.FromStoreId, vm.ToStoreId }
            );

            var fromStore = stores.FirstOrDefault(x => x.StoreId == vm.FromStoreId)
                ?? throw new Exception("Kho nguồn không tồn tại");

            var toStore = stores.FirstOrDefault(x => x.StoreId == vm.ToStoreId)
                ?? throw new Exception("Kho đích không tồn tại");

            using var tran = await _documentRepository.BeginTransactionAsync();

            try
            {
                // ✅ FIX: dùng đúng Items
                var items = await ConvertTransferItems(vm.Items);

                // 🔥 check tồn kho bên xuất
                await ValidateStock(vm.FromStoreId,
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

                // ================= EXPORT DOC =================
                var exportDoc = CreateDocument(new InventoryDocumentVM
                {
                    StoreId = vm.FromStoreId,
                    Type = InventoryDocumentType.EXPORT,
                    Purpose = InventoryDocumentPurpose.INTERNAL_OUT,
                    PartnerType = InventoryPartnerType.STORE,
                    PartnerId = vm.ToStoreId,
                    PartnerName = toStore.Name,
                    Note = vm.Note,
                    Details = vm.Items.Select(i => new InventoryDocumentDetailCreateVM
                    {
                        IngredientId = i.IngredientId,
                        Quantity = i.Quantity,
                        UnitId = i.UnitId,
                        UnitPrice = i.UnitPrice,
                        Note = i.Note
                    }).ToList()
                }, staffId,
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
                )).ToList());

                await _documentRepository.AddAsync(exportDoc);
                await _documentRepository.SaveChangesAsync();

                await UpdateStockAndTransaction(
                    new InventoryDocumentVM
                    {
                        Type = InventoryDocumentType.EXPORT,
                        StoreId = vm.FromStoreId
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

                // ================= IMPORT DOC (pending) =================
                var importDoc = CreateDocument(new InventoryDocumentVM
                {
                    StoreId = vm.ToStoreId,
                    Type = InventoryDocumentType.IMPORT,
                    Purpose = InventoryDocumentPurpose.IMPORT_INTERNAL,
                    PartnerType = InventoryPartnerType.STORE,
                    PartnerId = vm.FromStoreId,
                    PartnerName = fromStore.Name,
                    Note = vm.Note,
                    Details = vm.Items.Select(i => new InventoryDocumentDetailCreateVM
                    {
                        IngredientId = i.IngredientId,
                        Quantity = i.Quantity,
                        UnitId = i.UnitId,
                        UnitPrice = i.UnitPrice,
                        Note = i.Note
                    }).ToList()
                }, staffId,
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
                )).ToList());

                // 🔥 IMPORT chưa nhận
                importDoc.Status = InventoryDocumentStatus.DRAFT;

                await _documentRepository.AddAsync(importDoc);
                await _documentRepository.SaveChangesAsync();

                // ================= TRANSFER =================
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

                    // 🔥 NEW: DETAILS
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

            if (transfer.Status != InventoryTransferStatus.PENDING)
                throw new Exception("Chỉ confirm khi đang PENDING");

            if (transfer.ImportDocumentId == null)
                throw new Exception("Transfer chưa có phiếu nhập");

            using var tran = await _documentRepository.BeginTransactionAsync();

            try
            {
                var importDoc = await _repository.GetDocumentWithDetailsAsync(transfer.ImportDocumentId.Value);

                decimal totalReceived = 0;

                foreach (var detail in transfer.Details)
                {
                    var remain = detail.ExportQuantity - detail.ReceivedQuantity;

                    if (remain <= 0) continue;

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

                    stock.AvailableQty += remain;

                    var after = stock.AvailableQty;

                    totalReceived += remain;

                    // 🔥 update detail
                    detail.ReceivedQuantity += remain;

                    await _documentRepository.AddTransactionAsync(new InventoryTransaction
                    {
                        StoreInventory = stock,
                        Type = InventoryDocumentType.IMPORT,
                        Quantity = remain,
                        BeforeQty = before,
                        AfterQty = after,
                        InventoryDocumentId = transfer.ImportDocumentId,
                        CreatedAt = DateTime.Now
                    });
                }

                // 🔥 tính lại total
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

        public async Task ReceiveTransferAsync(int transferId, List<InventoryTransferReceiveItemVM> receivedItems)
        {
            var transfer = await _repository.GetTransferByIdAsync(transferId)
                ?? throw new Exception("Transfer không tồn tại");

            if (transfer.Status == InventoryTransferStatus.CANCELLED ||
                transfer.Status == InventoryTransferStatus.COMPLETED)
                throw new Exception("Transfer không hợp lệ");

            if (transfer.ImportDocumentId == null)
                throw new Exception("Transfer chưa có phiếu nhập");

            using var tran = await _documentRepository.BeginTransactionAsync();

            try
            {
                var importDoc = await _repository.GetDocumentWithDetailsAsync(transfer.ImportDocumentId.Value);

                foreach (var item in receivedItems)
                {
                    var detail = transfer.Details
                        .FirstOrDefault(x => x.IngredientId == item.IngredientId)
                        ?? throw new Exception($"Ingredient {item.IngredientId} không tồn tại trong transfer");

                    var remain = detail.ExportQuantity - detail.ReceivedQuantity;

                    if (item.BaseQuantity <= 0)
                        throw new Exception("Số lượng nhận không hợp lệ");

                    if (item.BaseQuantity > remain)
                        throw new Exception($"Nhận vượt cho ingredient {item.IngredientId}");

                    var stock = await _documentRepository.GetStoreInventoryAsync(
                        transfer.ToStoreId,
                        item.IngredientId
                    );

                    if (stock == null)
                    {
                        stock = new StoreInventory
                        {
                            StoreId = transfer.ToStoreId,
                            IngredientId = item.IngredientId,
                            AvailableQty = 0
                        };

                        await _documentRepository.AddStoreInventoryAsync(stock);
                    }

                    var before = stock.AvailableQty;

                    stock.AvailableQty += item.BaseQuantity;

                    var after = stock.AvailableQty;

                    // 🔥 update detail
                    detail.ReceivedQuantity += item.BaseQuantity;

                    await _documentRepository.AddTransactionAsync(new InventoryTransaction
                    {
                        StoreInventory = stock,
                        Type = InventoryDocumentType.IMPORT,
                        Quantity = item.BaseQuantity,
                        BeforeQty = before,
                        AfterQty = after,
                        InventoryDocumentId = transfer.ImportDocumentId,
                        CreatedAt = DateTime.Now
                    });
                }

                // 🔥 recompute total
                transfer.TotalReceivedQty = transfer.Details.Sum(x => x.ReceivedQuantity);

                // 🔥 STATUS CHUẨN ERP
                if (transfer.TotalReceivedQty == 0)
                {
                    transfer.Status = InventoryTransferStatus.PENDING;
                    importDoc.Status = InventoryDocumentStatus.DRAFT;
                }
                else if (transfer.TotalReceivedQty < transfer.TotalExportQty)
                {
                    transfer.Status = InventoryTransferStatus.IN_PROGRESS;
                    importDoc.Status = InventoryDocumentStatus.DRAFT;
                }
                else if (transfer.TotalReceivedQty == transfer.TotalExportQty)
                {
                    transfer.Status = InventoryTransferStatus.COMPLETED;
                    importDoc.Status = InventoryDocumentStatus.CONFIRMED;
                }

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

        public async Task<List<Store>> GetAvailableTransferSources(int storeId)
        {
            return await _repository.GetStoresHasPendingTransferToStore(storeId);
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
                _ => "DOC"
            };

            return $"{prefix}-{DateTime.Now:yyyyMMddHHmmssfff}";
        }
    }
}
