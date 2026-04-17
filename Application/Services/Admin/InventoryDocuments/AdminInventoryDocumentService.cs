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
                    PartnerName = x.PartnerName,
                    IngredientCode = x.Details.FirstOrDefault()?.Ingredient.Code ?? "",
                    Purpose = x.Purpose,
                    Type = x.Type,
                    Status = x.Status.ToString().ToUpper(),
                    Date = x.DocumentDate,
                    Note = x.Note,
                    TotalQuantity = x.Details.Sum(d => d.BaseQuantity),
                    BaseUnitName = x.Details.FirstOrDefault()?.Ingredient.BaseUnit.Name ?? "",
                    TotalAmount = x.Details.Sum(d => d.TotalAmount ?? 0),

                    Details = x.Details.Select(d => new InventoryDocumentDetailItemVM
                    {
                        IngredientCode = d.Ingredient.Code,
                        IngredientName = d.Ingredient.Name,
                        BaseUnitName = d.Ingredient.BaseUnit.Name,
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
                PartnerName = x.PartnerName,
                Purpose = x.Purpose,


                Type = x.Type.ToString(),
                Status = x.Status.ToString(),
                Date = x.DocumentDate,
                Note = x.Note,

                Details = x.Details.Select(d => new InventoryDocumentDetailItemVM
                {
                    IngredientCode = d.Ingredient.Code,
                    IngredientName = d.Ingredient.Name,
                    BaseUnitName = d.Ingredient.BaseUnit.Name,
                    Quantity = d.Quantity,
                    UnitName = d.Unit.Name,
                    BaseQuantity = d.BaseQuantity,
                    UnitPrice = d.UnitPrice,
                    TotalAmount = d.TotalAmount,
                    Note = d.Note,
                }).ToList()
            };
        }

        // ======================== CREATE ========================
        public async Task CreateAsync(InventoryDocumentVM vm)
        {
            Validate(vm);
            await NormalizeByPurpose(vm); // 🔥 FIX async
            ValidateBusiness(vm);
            ValidateTypeAndPurpose(vm);

            var staffId = _userContext.StaffId;

            var hasPermission = await _repository.CheckStaffHasStoreAsync(staffId, vm.StoreId);
            if (!hasPermission)
                throw new Exception("Bạn không có quyền thao tác kho này");

            using var tran = await _repository.BeginTransactionAsync();

            try
            {
                var convertedItems = await ConvertItems(vm);

                var document = vm.Type switch
                {
                    InventoryDocumentType.IMPORT => await ProcessImport(vm, staffId, convertedItems),
                    InventoryDocumentType.EXPORT => await ProcessExport(vm, staffId, convertedItems),
                    InventoryDocumentType.WASTE => await ProcessWaste(vm, staffId, convertedItems),
                    InventoryDocumentType.STOCK_TAKE => await ProcessStockTake(vm, staffId, convertedItems),
                    _ => throw new Exception("Type không hợp lệ")
                };

                await _repository.SaveChangesAsync();
                await tran.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tran.RollbackAsync();
                throw new Exception("Dữ liệu đã bị thay đổi, thử lại");
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }


        // ======================== IMPORT INFO ========================
        public async Task<ImportInfoDTO> GetImportInfoAsync(int ingredientId, int supplierId)
        {
            var item = await _repository.GetIngredientSupplierAsync(ingredientId, supplierId)
                ?? throw new Exception("Không tìm thấy cấu hình nhập");

            return new ImportInfoDTO
            {
                UnitId = item.UnitId,
                UnitName = item.Unit.Name,
                Price = item.Price
            };
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

        // ======================== INGREDIENT SUPPLIERS BY SUPPLIER ========================
        public async Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId)
        {
            return await _repository.GetIngredientSuppliersBySupplierAsync(supplierId);
        }

        // ========================= STORE INVENTORY ========================
        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId, bool onlyAvailable = false)
        {
            return await _repository.GetStoreInventoriesAsync(storeId, onlyAvailable);
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

            foreach (var item in vm.Details)
            {
                if (item.Quantity <= 0)
                    throw new Exception("Số lượng không hợp lệ");
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

        // CHUYỂN ĐỔI SỐ LƯỢNG VỀ ĐƠN VỊ CƠ SỞ
        private async Task<List<(InventoryDocumentDetailCreateVM item, decimal baseQty)>> ConvertItems(InventoryDocumentVM vm)
        {
            var ingredientDict = new Dictionary<int, Ingredient>();
            var result = new List<(InventoryDocumentDetailCreateVM, decimal)>();

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

                result.Add((item, baseQty));
            }

            return result;
        }

        // XỬ LÝ NHẬP KHO
        private async Task<InventoryDocument> ProcessImport(InventoryDocumentVM vm, int staffId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            var document = CreateDocument(vm, staffId, items);

            await _repository.AddAsync(document);
            await _repository.SaveChangesAsync();

            await UpdateStockAndTransaction(vm, document, items);

            return document;
        }

        // XỬ LÝ XUẤT KHO
        private async Task<InventoryDocument> ProcessExport(InventoryDocumentVM vm, int staffId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            await ValidateStock(vm.StoreId, items);

            var document = CreateDocument(vm, staffId, items);

            await _repository.AddAsync(document);
            await _repository.SaveChangesAsync();

            await UpdateStockAndTransaction(vm, document, items);

            await CreateDebtIfNeeded(vm, document);

            return document;
        }

        // XỬ LÝ HỦY HÀNG
        private async Task<InventoryDocument> ProcessWaste(InventoryDocumentVM vm, int staffId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            await ValidateStock(vm.StoreId, items);

            var document = CreateDocument(vm, staffId, items);

            await _repository.AddAsync(document);
            await _repository.SaveChangesAsync();

            await UpdateStockAndTransaction(vm, document, items);

            return document;
        }

        // XỬ LÝ KIỂM KÊ TỒN KHO
        private async Task<InventoryDocument> ProcessStockTake(InventoryDocumentVM vm, int staffId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            var document = CreateDocument(vm, staffId, items);

            await _repository.AddAsync(document);
            await _repository.SaveChangesAsync();

            foreach (var (item, baseQty) in items)
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

                // 🔥 STOCK TAKE: set về số kiểm kê
                stock.AvailableQty = baseQty;

                var afterQty = stock.AvailableQty;
                stock.LastUpdated = DateTime.Now;

                var diff = afterQty - beforeQty;

                // 🔥 chỉ tạo transaction nếu có chênh lệch
                if (diff != 0)
                {
                    var transaction = new InventoryTransaction
                    {
                        StoreInventory = stock,
                        Type = InventoryDocumentType.STOCK_TAKE,
                        Quantity = diff,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        InventoryDocument = document,
                        CreatedAt = DateTime.Now
                    };

                    await _repository.AddTransactionAsync(transaction);
                }
            }

            return document;
        }

        // TẠO PHIẾU NHẬP/XUẤT/HỦY
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

        // CẬP NHẬT TỒN KHO VÀ GHI NHẬT KÝ GIAO DỊCH
        private async Task UpdateStockAndTransaction(InventoryDocumentVM vm, InventoryDocument document, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            foreach (var (item, baseQty) in items)
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
                    Type = vm.Type,
                    Quantity = vm.Type == InventoryDocumentType.IMPORT ? baseQty : -baseQty,
                    BeforeQty = beforeQty,
                    AfterQty = afterQty,
                    InventoryDocument = document,
                    CreatedAt = DateTime.Now
                };

                await _repository.AddTransactionAsync(transaction);
            }
        }

        // TẠO CÔNG NỢ (nếu có)
        private async Task CreateDebtIfNeeded(InventoryDocumentVM vm, InventoryDocument document)
        {
            if (vm.Type != InventoryDocumentType.EXPORT ||
                vm.Purpose != InventoryDocumentPurpose.DEBT)
                return;

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

        // KIỂM TRA TỒN KHO TRƯỚC KHI XUẤT KHO/HỦY HÀNG
        private async Task ValidateStock(int storeId, List<(InventoryDocumentDetailCreateVM item, decimal baseQty)> items)
        {
            var grouped = items
                .GroupBy(x => x.item.IngredientId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.baseQty));

            var stocks = await _repository.GetStoreInventoriesAsync(storeId);

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

        // KIỂM TRA NGHIỆP VỤ RIÊNG THEO LOẠI PHIẾU
        private void ValidateBusiness(InventoryDocumentVM vm)
        {
            switch (vm.Purpose)
            {
                // ================= IMPORT =================
                case InventoryDocumentPurpose.IMPORT_PURCHASE:
                    if (!vm.SupplierId.HasValue)
                        throw new Exception("Nhập mua phải có nhà cung cấp");
                    break;

                case InventoryDocumentPurpose.IMPORT_INTERNAL:
                    if (vm.PartnerId == null)
                        throw new Exception("Nhập nội bộ phải có kho nguồn");
                    break;

                // ================= EXPORT =================
                case InventoryDocumentPurpose.SALE:
                case InventoryDocumentPurpose.DEBT:
                    if (vm.PartnerId == null && string.IsNullOrEmpty(vm.PartnerName))
                        throw new Exception("Xuất bán phải có khách hàng");
                    break;

                case InventoryDocumentPurpose.INTERNAL_OUT:
                    if (vm.PartnerId == null)
                        throw new Exception("Xuất nội bộ phải có kho đích");
                    break;
            }
        }

        // CHUẨN HÓA DỮ LIỆU ĐỐI TƯỢNG THEO MỤC ĐÍCH PHIẾU
        private async Task NormalizeByPurpose(InventoryDocumentVM vm)
        {
            switch (vm.Purpose)
            {
                // ================= IMPORT =================
                case InventoryDocumentPurpose.IMPORT_PURCHASE:
                    vm.PartnerType = InventoryPartnerType.SUPPLIER;
                    vm.PartnerId ??= vm.SupplierId;
                    if (string.IsNullOrEmpty(vm.PartnerName))
                    {
                        var supplier = await _repository.GetSupplierByIdAsync(vm.SupplierId.Value);
                        vm.PartnerName = supplier?.Name;
                    }
                    break;

                case InventoryDocumentPurpose.IMPORT_INTERNAL:
                    vm.SupplierId = null;
                    vm.PartnerType = InventoryPartnerType.STORE;
                    break;

                case InventoryDocumentPurpose.IMPORT_ADJUSTMENT:
                    vm.SupplierId = null;
                    vm.PartnerType = InventoryPartnerType.NONE;
                    vm.PartnerId = null;
                    vm.PartnerName = null;
                    break;

                // ================= EXPORT =================
                case InventoryDocumentPurpose.SALE:
                case InventoryDocumentPurpose.DEBT:
                    vm.PartnerType = InventoryPartnerType.CUSTOMER;
                    break;

                case InventoryDocumentPurpose.INTERNAL_OUT:
                    vm.PartnerType = InventoryPartnerType.STORE;
                    break;

                case InventoryDocumentPurpose.GIFT:
                case InventoryDocumentPurpose.SAMPLE:
                case InventoryDocumentPurpose.ADJUSTMENT_OUT:
                    vm.PartnerType = InventoryPartnerType.NONE;
                    vm.PartnerId = null;
                    vm.PartnerName = null;
                    break;

                // ================= WASTE =================
                case InventoryDocumentPurpose.DAMAGED:
                case InventoryDocumentPurpose.EXPIRED:
                case InventoryDocumentPurpose.BROKEN:
                case InventoryDocumentPurpose.CONTAMINATED:
                case InventoryDocumentPurpose.LOST:
                    vm.SupplierId = null;
                    vm.PartnerType = InventoryPartnerType.NONE;
                    vm.PartnerId = null;
                    vm.PartnerName = null;
                    break;
            }
        }

        // KIỂM TRA SỰ PHÙ HỢP GIỮA LOẠI PHIẾU VÀ MỤC ĐÍCH
        private void ValidateTypeAndPurpose(InventoryDocumentVM vm)
        {
            var valid = vm.Type switch
            {
                InventoryDocumentType.IMPORT =>
                    vm.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE ||
                    vm.Purpose == InventoryDocumentPurpose.IMPORT_INTERNAL ||
                    vm.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT,

                InventoryDocumentType.EXPORT =>
                    vm.Purpose == InventoryDocumentPurpose.SALE ||
                    vm.Purpose == InventoryDocumentPurpose.DEBT ||
                    vm.Purpose == InventoryDocumentPurpose.INTERNAL_OUT ||
                    vm.Purpose == InventoryDocumentPurpose.GIFT ||
                    vm.Purpose == InventoryDocumentPurpose.SAMPLE ||
                    vm.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT,

                InventoryDocumentType.WASTE =>
                    vm.Purpose == InventoryDocumentPurpose.DAMAGED ||
                    vm.Purpose == InventoryDocumentPurpose.EXPIRED ||
                    vm.Purpose == InventoryDocumentPurpose.BROKEN ||
                    vm.Purpose == InventoryDocumentPurpose.CONTAMINATED ||
                    vm.Purpose == InventoryDocumentPurpose.LOST,

                InventoryDocumentType.STOCK_TAKE =>
                    vm.Purpose == InventoryDocumentPurpose.STOCK_TAKE,

                _ => false
            };

            if (!valid)
                throw new Exception("Type và Purpose không hợp lệ");
        }
    }
}
