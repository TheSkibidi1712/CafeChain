using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Admin.UnitConversions;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.UnitConversions
{
    /// <summary>#127 Admin UX for unit conversion / package semantics (additive validation).</summary>
    public interface IAdminUnitConversionService
    {
        Task<AdminUnitConversionIndexDto> GetIndexAsync(string? search = null, string? statusFilter = null);

        Task<AdminUnitConversionEvaluateResult> EvaluateAsync(AdminUnitConversionEvaluateRequest request);

        Task<ServiceResult<int>> CreateAsync(AdminUnitConversionEvaluateRequest request);

        Task<ServiceResult> UpdateAsync(AdminUnitConversionEvaluateRequest request);

        Task<ServiceResult> DeleteAsync(int unitConversionId);

        Task<AdminUnitConversionEvaluateRequest?> GetForEditAsync(int unitConversionId);

        Task<List<AdminIngredientOptionDto>> GetIngredientOptionsAsync(string? search = null);

        Task<List<AdminUnitOptionDto>> GetUnitOptionsAsync();

        List<PhysicalStandardDto> GetPhysicalStandards();
    }
}
