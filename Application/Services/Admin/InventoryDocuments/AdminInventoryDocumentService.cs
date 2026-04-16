using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentService : IAdminInventoryDocumentService
    {
        private readonly IAdminInventoryDocumentRepository _repository;
        private readonly IUserContext _userContext;


        public AdminInventoryDocumentService(IAdminInventoryDocumentRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        // ======================== CREATE DATA ========================
        public async Task<InventoryDocumentCreateVM> GetCreateDataAsync()
        {
            var staffId = _userContext.StaffId;

            return new InventoryDocumentCreateVM
            {
                Stores = await _repository.GetStoresByStaffAsync(staffId),
                Suppliers = await _repository.GetSuppliersAsync(),

                Ingredients = new List<IngredientDropdownDTO>(),
                Units = new List<UnitDropdownDTO>(),

                Form = new InventoryDocumentVM
                {
                    DocumentDate = DateTime.Now
                }
            };
        }

        // ======================== PAGED ========================
        public async Task<InventoryDocumentIndexVM> GetPagedAsync(InventoryDocumentFilterDTO filter)
        {
            var (data, total) = await _repository.GetPagedAsync(filter);

            return new InventoryDocumentIndexVM
            {
                Items = data.Select(x => new InventoryDocumentItemVM
                {
                    Id = x.InventoryDocumentId,
                    Code = x.Code,
                    StoreName = x.Store.Name,
                    StaffName = x.Staff.FullName,
                    SupplierName = x.Supplier != null ? x.Supplier.Name : "",

                    Type = x.Type,
                    Status = x.Status.ToString().ToUpper(),
                    Date = x.DocumentDate,
                    Note = x.Note,
                    TotalQuantity = x.Details.Sum(d => d.BaseQuantity),
                    TotalAmount = x.Details.Sum(d => d.Quantity * (d.UnitPrice ?? 0)),

                    Details = x.Details.Select(d => new InventoryDocumentDetailItemVM
                    {
                        IngredientName = d.Ingredient.Name,
                        Quantity = d.Quantity,
                        UnitName = d.Unit.Name,
                        BaseQuantity = d.BaseQuantity,
                        UnitPrice = d.UnitPrice,
                        Note = d.Note,
                    }).ToList()
                }).ToList(),

                TotalRecords = total,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        // ======================== DETAIL ========================
        public async Task<InventoryDocumentDetailVM?> GetDetailAsync(int id)
        {
            var x = await _repository.GetDetailAsync(id);
            if (x == null) return null;

            return new InventoryDocumentDetailVM
            {
                Id = x.InventoryDocumentId,
                Code = x.Code,
                StoreName = x.Store.Name,
                StaffName = x.Staff.FullName,
                SupplierName = x.Supplier != null ? x.Supplier.Name : "",

                Type = x.Type.ToString(),
                Status = x.Status.ToString(),
                Date = x.DocumentDate,
                Note = x.Note,

                Details = x.Details.Select(d => new InventoryDocumentDetailItemVM
                {
                    IngredientName = d.Ingredient.Name,
                    Quantity = d.Quantity,
                    UnitName = d.Unit.Name,
                    BaseQuantity = d.BaseQuantity,
                    UnitPrice = d.UnitPrice,
                    Note = d.Note,
                }).ToList()
            };
        }

        // ======================== CREATE ========================
        public async Task CreateAsync(InventoryDocumentVM vm)
        {
            Validate(vm);

            var staffId = _userContext.StaffId;

            // 🔥 CHECK quyền store
            var hasPermission = await _repository.CheckStaffHasStoreAsync(staffId, vm.StoreId);
            if (!hasPermission)
                throw new Exception("Bạn không có quyền thao tác kho này");

            using var tran = await _repository.BeginTransactionAsync();

            try
            {
                var ingredientDict = new Dictionary<int, Ingredient>();
                var convertedItems = new List<(InventoryDocumentDetailCreateVM item, decimal baseQty)>();

                // ================= STEP 1: CONVERT =================
                foreach (var item in vm.Details)
                {
                    if (!ingredientDict.ContainsKey(item.IngredientId))
                    {
                        var ing = await _repository.GetIngredientAsync(item.IngredientId)
                            ?? throw new Exception($"Không tìm thấy nguyên liệu {item.IngredientId}");

                        ingredientDict[item.IngredientId] = ing;
                    }

                    var ingre = ingredientDict[item.IngredientId];
                    var baseQty = await ConvertToBaseUnit(item, ingre.BaseUnitId);

                    convertedItems.Add((item, baseQty));
                }

                // ================= STEP 2: VALIDATE STOCK =================
                await ValidateStockBeforeProcess(
                    vm.StoreId,
                    convertedItems.Select(x => (x.item.IngredientId, x.baseQty)).ToList(),
                    vm.Type
                );

                // ================= STEP 3: CREATE DOCUMENT =================
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

                foreach (var (item, baseQty) in convertedItems)
                {
                    document.Details.Add(new InventoryDocumentDetail
                    {
                        IngredientId = item.IngredientId,
                        UnitId = item.UnitId,
                        Quantity = item.Quantity,
                        BaseQuantity = baseQty,
                        UnitPrice = vm.Type == InventoryDocumentType.IMPORT
                            || vm.Purpose == InventoryDocumentPurpose.DEBT
                                ? item.UnitPrice
                                : null,
                        Note = item.Note
                    });
                }

                await _repository.AddAsync(document);
                await _repository.SaveChangesAsync(); // lấy ID

                // ================= STEP 4: STOCK + TRANSACTION =================
                foreach (var (item, baseQty) in convertedItems)
                {
                    var stock = await _repository.GetStoreInventoryAsync(vm.StoreId, item.IngredientId);

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

                        await _repository.AddStoreInventoryAsync(stock);
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
                        InventoryTransactionTypeId =
                            vm.Type == InventoryDocumentType.IMPORT ? 1 :
                            vm.Type == InventoryDocumentType.EXPORT ? 2 : 4,

                        Quantity = vm.Type == InventoryDocumentType.IMPORT ? baseQty : -baseQty,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        InventoryDocument = document,
                        CreatedAt = DateTime.Now
                    };

                    await _repository.AddTransactionAsync(transaction);
                }

                // ================= STEP 5: CREATE DEBT =================
                if (vm.Type == InventoryDocumentType.EXPORT
                    && vm.Purpose == InventoryDocumentPurpose.DEBT)
                {
                    var totalAmount = document.Details.Sum(x => x.Quantity * (x.UnitPrice ?? 0));

                    if (totalAmount <= 0)
                        throw new Exception("Phiếu nợ phải có giá");

                    var debt = new InventoryDebt
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        PartnerType = vm.PartnerType,
                        PartnerId = vm.PartnerId,
                        PartnerName = vm.PartnerName ?? "Khách lẻ",

                        Amount = totalAmount,
                        PaidAmount = 0,

                        CreatedAt = DateTime.Now
                    };

                    await _repository.AddDebtAsync(debt);
                }

                await _repository.SaveChangesAsync();
                await tran.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tran.RollbackAsync();
                throw new Exception("Dữ liệu đã bị thay đổi bởi người khác, vui lòng thử lại");
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        // ======================== STOCK TAKE ========================
        public async Task CreateStockTakeAsync(int storeId, List<StockTakeItemVM> items)
        {
            if (storeId <= 0)
                throw new Exception("Store không hợp lệ");

            if (items == null || !items.Any())
                throw new Exception("Danh sách kiểm kê trống");

            var staffId = _userContext.StaffId;

            using var tran = await _repository.BeginTransactionAsync();

            try
            {
                var document = new InventoryDocument
                {
                    Code = GenerateCode(InventoryDocumentType.STOCK_TAKE),
                    StoreId = storeId,
                    StaffId = staffId,
                    Type = InventoryDocumentType.STOCK_TAKE,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    Purpose = InventoryDocumentPurpose.NONE,
                    DocumentDate = DateTime.Now,
                    Details = new List<InventoryDocumentDetail>()
                };

                foreach (var item in items)
                {
                    if (item.ActualQty < 0)
                        throw new Exception($"Số lượng không hợp lệ (IngredientId={item.IngredientId})");

                    var ingredient = await _repository.GetIngredientAsync(item.IngredientId)
                        ?? throw new Exception($"Không tìm thấy nguyên liệu {item.IngredientId}");

                    var stock = await _repository.GetStoreInventoryAsync(storeId, item.IngredientId);

                    var systemQty = stock?.AvailableQty ?? 0;
                    var diff = item.ActualQty - systemQty;

                    // Không thay đổi thì skip
                    if (diff == 0) continue;

                    // tạo stock nếu chưa có
                    if (stock == null)
                    {
                        stock = new StoreInventory
                        {
                            StoreId = storeId,
                            IngredientId = item.IngredientId,
                            AvailableQty = 0,
                            ReservedQty = 0,
                            LastUpdated = DateTime.Now
                        };

                        await _repository.AddStoreInventoryAsync(stock);
                    }

                    var before = stock.AvailableQty;

                    // 🔥 overwrite theo thực tế
                    stock.AvailableQty = item.ActualQty;
                    stock.LastUpdated = DateTime.Now;

                    var after = stock.AvailableQty;

                    // ✅ DETAIL (dùng base unit)
                    document.Details.Add(new InventoryDocumentDetail
                    {
                        IngredientId = item.IngredientId,
                        Quantity = diff,
                        BaseQuantity = diff,
                        UnitId = ingredient.BaseUnitId, // 🔥 CHUẨN
                        Note = $"System={systemQty} | Actual={item.ActualQty}"
                    });

                    // ✅ TRANSACTION
                    await _repository.AddTransactionAsync(new InventoryTransaction
                    {
                        StoreInventory = stock,
                        InventoryTransactionTypeId = (int)InventoryTransactionTypeEnum.STOCK_TAKE,
                        Quantity = diff,
                        BeforeQty = before,
                        AfterQty = after,
                        InventoryDocument = document,
                        CreatedAt = DateTime.Now
                    });
                }

                // Không có thay đổi → không tạo phiếu
                if (!document.Details.Any())
                    throw new Exception("Không có thay đổi tồn kho");

                await _repository.AddAsync(document);
                await _repository.SaveChangesAsync();

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        // ======================== CANCEL ========================
        public async Task CancelAsync(int documentId)
        {
            var doc = await _repository.GetDetailAsync(documentId)
                ?? throw new Exception("Không tìm thấy phiếu");

            if (doc.Status == InventoryDocumentStatus.CANCELLED)
                throw new Exception("Đã hủy");

            using var tran = await _repository.BeginTransactionAsync();

            try
            {
                var reversal = new InventoryDocument
                {
                    Code = "RV-" + DateTime.Now.Ticks,
                    StoreId = doc.StoreId,
                    StaffId = _userContext.StaffId,
                    Type = doc.Type,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    DocumentDate = DateTime.Now,
                    IsReversal = true,
                    RefDocumentId = doc.InventoryDocumentId,
                    Details = new List<InventoryDocumentDetail>()
                };

                foreach (var d in doc.Details)
                {
                    var stock = await _repository.GetStoreInventoryAsync(doc.StoreId, d.IngredientId);

                    var before = stock.AvailableQty;

                    var delta = doc.Type == InventoryDocumentType.IMPORT
                        ? -d.BaseQuantity
                        : d.BaseQuantity;

                    stock.AvailableQty += delta;

                    var after = stock.AvailableQty;

                    reversal.Details.Add(new InventoryDocumentDetail
                    {
                        IngredientId = d.IngredientId,
                        Quantity = -d.Quantity,
                        BaseQuantity = delta,
                        UnitId = d.UnitId
                    });

                    await _repository.AddTransactionAsync(new InventoryTransaction
                    {
                        StoreInventory = stock,
                        InventoryTransactionTypeId = (int)doc.Type,
                        Quantity = delta,
                        BeforeQty = before,
                        AfterQty = after,
                        CreatedAt = DateTime.Now
                    });
                }

                doc.Status = InventoryDocumentStatus.CANCELLED;

                await _repository.AddAsync(reversal);
                await _repository.SaveChangesAsync();

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        // ======================== STOCK ========================
        public async Task<decimal> GetStockAsync(int storeId, int ingredientId)
        {
            var stock = await _repository.GetStoreInventoryAsync(storeId, ingredientId);
            return stock?.AvailableQty ?? 0;
        }

        // ======================== UNITS ========================
        public async Task<List<Unit>> GetUnitsByIngredientAsync(int ingredientId)
        {
            return await _repository.GetUnitsByIngredientAsync(ingredientId);
        }

        // ======================== UNITS WITH PRICE ========================
        public async Task<(int unitId, string unitName, decimal price)> GetImportInfoAsync(int ingredientId, int supplierId)
        {
            var data = await _repository.GetIngredientSuppliersAsync(ingredientId, supplierId);

            var item = data.FirstOrDefault();

            if (item == null)
                throw new Exception("Không tìm thấy cấu hình nhập");

            return (
                item.UnitId,
                item.Unit.Name,
                item.Price
            );
        }

        // ======================== INGREDIENT SUPPLIERS BY SUPPLIER ========================
        public async Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId)
        {
            return await _repository.GetIngredientSuppliersBySupplierAsync(supplierId);
        }


        // ========================= CHO XUẤT KHO =========================
        // ========================= STORE INVENTORY ========================
        public async Task<List<StoreInventory>> GetIngredientsByStoreAsync(int storeId)
        {
            return await _repository.GetStoreInventoriesAsync(storeId);
        }

        public async Task<List<StoreInventory>> GetIngredientsForExportAsync(int storeId)
        {
            return await _repository.GetStoreInventoriesForExportAsync(storeId);
        }

        // ======================== PRIVATE ========================
        private async Task<decimal> ConvertToBaseUnit(InventoryDocumentDetailCreateVM item, int baseUnitId)
        {
            if (item.UnitId == baseUnitId)
                return item.Quantity;

            var conversion = await _repository.GetConversionAsync(
                item.IngredientId,
                item.UnitId,
                baseUnitId
            );

            if (conversion == null)
                throw new Exception($"Không có quy đổi đơn vị cho nguyên liệu {item.IngredientId}");

            return item.Quantity * (conversion.ToQuantity / conversion.FromQuantity);
        }

        private void Validate(InventoryDocumentVM vm)
        {
            if (vm.StoreId <= 0)
                throw new Exception("Store không hợp lệ");

            if (!vm.Details.Any())
                throw new Exception("Danh sách nguyên liệu trống");

            if (vm.Type == InventoryDocumentType.IMPORT && !vm.SupplierId.HasValue)
                throw new Exception("Phiếu nhập phải chọn nhà cung cấp");

            foreach (var item in vm.Details)
            {
                if (item.Quantity <= 0)
                    throw new Exception("Số lượng phải > 0");
            }
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

        private async Task ValidateStockBeforeProcess(int storeId, List<(int ingredientId, decimal baseQty)> items, InventoryDocumentType type)
        {
            if (type == InventoryDocumentType.IMPORT)
                return;

            foreach (var item in items)
            {
                var stock = await _repository.GetStoreInventoryAsync(storeId, item.ingredientId);
                var available = stock?.AvailableQty ?? 0;

                if (available < item.baseQty)
                {
                    throw new Exception(
                        $"Không đủ tồn kho (IngredientId={item.ingredientId}) - Tồn: {available}, Cần: {item.baseQty}"
                    );
                }
            }
        }
    }
}
