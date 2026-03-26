using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.Models.Inventories;

namespace CafeChain.Infrastrusture.Repositories.Admin
{
    public class AdminIngredientRepository : IAdminIngredientRepository
    {
        private readonly AppDbContext _context;

        public AdminIngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public IQueryable<Ingredient> GetAllIngredientsWithStock()
        {
            return _context.Ingredients
                .Include(i => i.StoreInventories);
        }

        public async Task<int> GetActiveIngredientsCountAsync()
        {
            return await _context.Ingredients.CountAsync(i => i.Active);
        }

        public async Task<int> GetLowStockIngredientsCountAsync(decimal threshold)
        {
            // Assuming an ingredient is low stock if its total available quantity across all stores is less than the threshold
            var ingredientsStock = await _context.Ingredients
                .Where(i => i.Active)
                .Select(i => new 
                {
                    TotalStock = i.StoreInventories.Sum(si => si.AvailableQty)
                })
                .ToListAsync();

            return ingredientsStock.Count(x => x.TotalStock < threshold);
        }

        public async Task<decimal> GetTotalInventoryValueAsync()
        {
            // Calculate value based on total stock * last import price for each ingredient
            // Since there is no direct price on Ingredient, we approximate by total stock * average import price
            var ingredients = await _context.Ingredients
                .Where(i => i.Active && i.StoreInventories.Any())
                .Select(i => new
                {
                    TotalStock = i.StoreInventories.Sum(s => s.AvailableQty),
                    AvgImportPrice = i.StockImportDetails.Any() 
                        ? i.StockImportDetails.Average(d => d.UnitPrice) 
                        : 0
                })
                .ToListAsync();

            return ingredients.Sum(i => i.TotalStock * i.AvgImportPrice);
        }

        public async Task<int> GetMonthlyImportBatchesCountAsync(int year, int month)
        {
            return await _context.StockImports
                .CountAsync(s => s.ImportDate.Year == year && s.ImportDate.Month == month);
        }

        public async Task<List<Ingredient>> GetAllActiveIngredientsAsync()
        {
            return await _context.Ingredients.Where(i => i.Active).ToListAsync();
        }

        public async Task<bool> CreateStockImportAsync(StockImport stockImport)
        {
            await _context.StockImports.AddAsync(stockImport);

            // Need to update StoreInventories
            foreach (var detail in stockImport.Details)
            {
                var storeInventory = await _context.StoreInventories
                    .FirstOrDefaultAsync(si => si.StoreId == stockImport.StoreId && si.IngredientId == detail.IngredientId);

                if (storeInventory != null)
                {
                    storeInventory.AvailableQty += detail.Quantity;
                    storeInventory.LastUpdated = DateTime.Now;
                }
                else
                {
                    await _context.StoreInventories.AddAsync(new StoreInventory
                    {
                        StoreId = stockImport.StoreId,
                        IngredientId = detail.IngredientId,
                        AvailableQty = detail.Quantity,
                        ReservedQty = 0,
                        LastUpdated = DateTime.Now
                    });
                }
            }

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
    }
}
