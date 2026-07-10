using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #98 — POS shortage report for StoreManager / AccountantWarehouse.</summary>
    public interface IStockShortageReportService
    {
        Task<ServiceResult<StockShortageReportResultDto>> ReportShortageAsync(
            int storeId,
            int reportedByStaffId,
            StockShortageReportRequestDto request);
    }
}
