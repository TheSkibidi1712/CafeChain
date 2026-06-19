using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Transfers;
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
        }
    }
