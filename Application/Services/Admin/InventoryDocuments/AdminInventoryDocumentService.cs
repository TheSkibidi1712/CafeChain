using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentService : IAdminInventoryDocumentService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        public AdminInventoryDocumentService(IAdminInventoryDocumentRepository repository)
        {
            _repository = repository;
        }

        // ======================== CREATE DATA ========================
        public async Task<InventoryDocumentCreateVM> GetCreateDataAsync()
        {
            return new InventoryDocumentCreateVM
            {
                Stores = await _repository.GetStoresAsync(),
                Staffs = await _repository.GetStaffsAsync(),
                Suppliers = await _repository.GetSuppliersAsync(),

                Ingredients = new List<IngredientDropdownDTO>(),

                Units = new List<UnitDropdownDTO>(),

                Form = new InventoryDocumentVM()
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
                    StaffId = vm.StaffId,
                    SupplierId = vm.SupplierId,
                    DocumentDate = DateTime.SpecifyKind(vm.DocumentDate, DateTimeKind.Local),
                    Type = vm.Type,
                    Status = InventoryDocumentStatus.CONFIRMED,
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
                        UnitPrice = item.UnitPrice,
                        Note = item.Note
                    });
                }

                await _repository.AddAsync(document);
                await _repository.SaveChangesAsync(); // ✅ cần để lấy ID

                var documentId = document.InventoryDocumentId;

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

                    // ✅ APPLY STOCK
                    switch (vm.Type)
                    {
                        case InventoryDocumentType.IMPORT:
                            stock.AvailableQty += baseQty;
                            break;

                        case InventoryDocumentType.EXPORT:
                        case InventoryDocumentType.WASTE:
                            if (stock.AvailableQty < baseQty)
                                throw new Exception($"Không đủ tồn kho (IngredientId={item.IngredientId})");

                            stock.AvailableQty -= baseQty;
                            break;
                    }

                    var afterQty = stock.AvailableQty;

                    stock.LastUpdated = DateTime.Now;

                    // ❌ KHÔNG update gì hết, EF tự track

                    var transaction = new InventoryTransaction
                    {
                        StoreInventory = stock, // ✅ QUAN TRỌNG (không dùng ID)
                        InventoryTransactionTypeId = vm.Type == InventoryDocumentType.IMPORT ? 1
                            : vm.Type == InventoryDocumentType.EXPORT ? 2 : 4,

                        Quantity = vm.Type == InventoryDocumentType.IMPORT ? baseQty : -baseQty,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        InventoryDocument = document, // ✅ QUAN TRỌNG
                        CreatedAt = DateTime.Now
                    };

                    await _repository.AddTransactionAsync(transaction);
                }

                // ✅ SAVE 1 LẦN DUY NHẤT
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

            if (vm.StaffId <= 0)
                throw new Exception("Staff không hợp lệ");

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
