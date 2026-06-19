using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MockPOSController : ControllerBase
    {
        private readonly IInventoryDeductionService _deductionService;

        public MockPOSController(IInventoryDeductionService deductionService)
        {
            _deductionService = deductionService;
        }

        /// <summary>
        /// Mô phỏng việc hoàn tất một đơn hàng trên màn hình POS.
        /// Gọi hàm này để kích hoạt Engine trừ kho tự động.
        /// </summary>
        [HttpPost("CompletePOSOrder")]
        public async Task<IActionResult> CompletePOSOrder([FromBody] List<POSSoldItemDto> soldItems, [FromQuery] int storeId)
        {
            if (soldItems == null || soldItems.Count == 0)
                return BadRequest(new { success = false, message = "Đơn hàng trống." });

            if (storeId <= 0)
                return BadRequest(new { success = false, message = "StoreId không hợp lệ." });

            var result = await _deductionService.DeductStockForOrderAsync(soldItems, storeId);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = result.Message });
            }
            else
            {
                return StatusCode(500, new { success = false, message = result.Message });
            }
        }
    }
}
