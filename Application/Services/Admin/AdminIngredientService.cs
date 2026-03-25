using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.DTOs.Admin.Inventory;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Infrastrusture.Interfaces.Admin;
using CafeChain.ViewModels.Admin.Inventories;
using CafeChain.ViewModels.Shared;

namespace CafeChain.Application.Services.Admin
{
    public class AdminIngredientService : IAdminIngredientService
    {
        private readonly IAdminIngredientRepository _repository;

        public AdminIngredientService(IAdminIngredientRepository repository)
        {
            _repository = repository;
        }

        public async Task<AdminInventoryListViewModel> GetInventoryDashboardAsync(int pageIndex, int pageSize, string searchTerm, string type, string status)
        {
            var query = _repository.GetAllIngredientsWithStock();

            // Search by Name or Code
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                // Since Code requires formatting, we primarily search by name or ID
                // Alternatively, we search by name and assume ID if numeric
                if (int.TryParse(searchTerm.Replace("MAT", "").Replace("CF", "").Replace("MK", "").Replace("SW", "").Replace("TP", "").Replace("-", ""), out int id))
                {
                    query = query.Where(i => i.Name.ToLower().Contains(lowerSearch) || i.IngredientId == id);
                }
                else
                {
                    query = query.Where(i => i.Name.ToLower().Contains(lowerSearch));
                }
            }

            var allIngredients = await query.ToListAsync();

            // Map to DTO
            var dtoList = allIngredients.Select(i => new AdminIngredientDto
            {
                IngredientId = i.IngredientId,
                Code = GetIngredientCode(i),
                Name = i.Name,
                Type = GetIngredientType(i.Name),
                Unit = i.Unit,
                TotalStock = i.StoreInventories.Sum(s => s.AvailableQty),
                Status = GetStockStatus(i.StoreInventories.Sum(s => s.AvailableQty))
            }).ToList();

            // Filter by Type
            if (!string.IsNullOrWhiteSpace(type) && type != "Tất cả loại")
            {
                dtoList = dtoList.Where(d => d.Type == type).ToList();
            }

            // Filter by Status
            if (!string.IsNullOrWhiteSpace(status) && status != "Trạng thái")
            {
                dtoList = dtoList.Where(d => d.Status == status).ToList();
            }

            // Pagination
            var count = dtoList.Count;
            var items = dtoList.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

            var paginatedList = new PaginatedListViewModel<AdminIngredientDto>(items, count, pageIndex, pageSize);

            // Fetch summary stats
            var totalItemsCount = await _repository.GetActiveIngredientsCountAsync();
            var lowStockCount = await _repository.GetLowStockIngredientsCountAsync(10); // threshold = 10
            var totalValue = await _repository.GetTotalInventoryValueAsync();
            var importBatches = await _repository.GetMonthlyImportBatchesCountAsync(DateTime.Now.Year, DateTime.Now.Month);

            var vm = new AdminInventoryListViewModel
            {
                TotalItemsCount = totalItemsCount,
                TotalItemsGrowthPercentage = 2.4, // Static mock for now based on UI requirements
                LowStockCount = lowStockCount,
                TotalInventoryValue = totalValue,
                MonthlyImportBatches = importBatches,
                SearchTerm = searchTerm,
                SelectedType = type,
                SelectedStatus = status,
                Types = new List<string> { "Tất cả loại", "Hạt Cafe", "Sữa & Kem", "Đường & Siro", "Topping", "Khác" },
                Statuses = new List<string> { "Trạng thái", "Ổn định", "Gần hết", "Sắp hết" },
                IngredientsList = paginatedList
            };

            return vm;
        }

        private string GetIngredientCode(Models.Inventories.Ingredient i)
        {
            var type = GetIngredientType(i.Name);
            var prefix = "MAT-KH";
            if (type == "Hạt Cafe") prefix = "MAT-CF";
            else if (type == "Sữa & Kem") prefix = "MAT-MK";
            else if (type == "Đường & Siro") prefix = "MAT-SW";
            else if (type == "Topping") prefix = "MAT-TP";

            return $"{prefix}-{i.IngredientId:D3}";
        }

        private string GetIngredientType(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Contains("cafe") || lowerName.Contains("cà phê") || lowerName.Contains("robusta") || lowerName.Contains("arabica"))
                return "Hạt Cafe";
            if (lowerName.Contains("sữa") || lowerName.Contains("kem") || lowerName.Contains("milk") || lowerName.Contains("cream"))
                return "Sữa & Kem";
            if (lowerName.Contains("đường") || lowerName.Contains("siro") || lowerName.Contains("syrup") || lowerName.Contains("sugar"))
                return "Đường & Siro";
            if (lowerName.Contains("trân châu") || lowerName.Contains("thạch") || lowerName.Contains("cacao") || lowerName.Contains("topping"))
                return "Topping";
            
            return "Khác";
        }

        private string GetStockStatus(decimal stock)
        {
            if (stock < 10) return "Sắp hết";
            if (stock < 20) return "Gần hết";
            return "Ổn định";
        }

        public async Task<byte[]> ExportInventoryCsvAsync(string searchTerm, string type, string status)
        {
            var query = _repository.GetAllIngredientsWithStock();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                if (int.TryParse(searchTerm.Replace("MAT", "").Replace("CF", "").Replace("MK", "").Replace("SW", "").Replace("TP", "").Replace("-", ""), out int id))
                {
                    query = query.Where(i => i.Name.ToLower().Contains(lowerSearch) || i.IngredientId == id);
                }
                else
                {
                    query = query.Where(i => i.Name.ToLower().Contains(lowerSearch));
                }
            }

            var allIngredients = await query.ToListAsync();

            var dtoList = allIngredients.Select(i => new AdminIngredientDto
            {
                IngredientId = i.IngredientId,
                Code = GetIngredientCode(i),
                Name = i.Name,
                Type = GetIngredientType(i.Name),
                Unit = i.Unit,
                TotalStock = i.StoreInventories.Sum(s => s.AvailableQty),
                Status = GetStockStatus(i.StoreInventories.Sum(s => s.AvailableQty))
            }).ToList();

            if (!string.IsNullOrWhiteSpace(type) && type != "Tất cả loại")
            {
                dtoList = dtoList.Where(d => d.Type == type).ToList();
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "Trạng thái")
            {
                dtoList = dtoList.Where(d => d.Status == status).ToList();
            }

            var csv = new StringBuilder();
            csv.AppendLine("Mã NL,Tên Nguyên Liệu,Loại,ĐVT,Tồn Kho,Trạng Thái");
            foreach (var item in dtoList)
            {
                csv.AppendLine($"{item.Code},\"{item.Name}\",{item.Type},{item.Unit},{item.TotalStock},{item.Status}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<List<AdminIngredientDropdownDto>> GetIngredientsForDropdownAsync()
        {
            var ingredients = await _repository.GetAllActiveIngredientsAsync();
            return ingredients.Select(i => new AdminIngredientDropdownDto
            {
                IngredientId = i.IngredientId,
                Name = i.Name,
                Unit = i.Unit
            }).ToList();
        }

        public async Task<bool> CreateStockImportAsync(AdminCreateStockImportDto dto, int storeId, int staffId)
        {
            if (dto == null || dto.Details == null || !dto.Details.Any()) return false;

            var stockImport = new Models.Inventories.StockImport
            {
                StoreId = storeId,
                StaffId = staffId,
                ImportDate = DateTime.Now,
                Note = dto.Note,
                Details = dto.Details.Select(d => new Models.Inventories.StockImportDetail
                {
                    IngredientId = d.IngredientId,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                }).ToList()
            };

            return await _repository.CreateStockImportAsync(stockImport);
        }
    }
}
