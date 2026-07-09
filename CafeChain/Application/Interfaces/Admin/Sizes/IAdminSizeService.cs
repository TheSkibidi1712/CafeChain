using CafeChain.Application.DTOs.Admin.Sizes;

namespace CafeChain.Application.Interfaces.Admin.Sizes
{
    public interface IAdminSizeService
    {
        Task<IEnumerable<SizeDto>> GetActiveSizesAsync();
        Task<SizeDto?> GetSizeByIdAsync(int id);
        Task<(bool Success, string Error)> CreateSizeAsync(SizeDto sizeDto);
        Task<(bool Success, string Error)> UpdateSizeAsync(SizeDto sizeDto);
        Task ToggleStatusAsync(int id); // Thay thế cho Delete
    }
}
