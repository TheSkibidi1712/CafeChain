using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/admin/inventory-documents")]
    [ApiController]
    public class AdminInventoryDocumentApiController : Controller
    {
        private readonly IAdminInventoryDocumentService _service;
        private readonly IAdminInventoryTransferService _transferService;

        public AdminInventoryDocumentApiController(IAdminInventoryDocumentService service, IAdminInventoryTransferService transferService)
        {
            _service = service;
            _transferService = transferService;
        }

    }
}