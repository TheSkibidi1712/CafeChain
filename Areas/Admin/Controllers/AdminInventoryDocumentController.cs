using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminInventoryDocumentController : AdminBaseController
    {
        private readonly IAdminInventoryDocumentService _service;

        public AdminInventoryDocumentController(IAdminInventoryDocumentService service)
        {
            _service = service;
        }
    }
}

