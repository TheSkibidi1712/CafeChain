using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CafeChain.Infrastrusture.Repositories.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentRepository : IAdminInventoryDocumentRepository
    {
        private readonly AppDbContext _context;

        public AdminInventoryDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

    }
}
