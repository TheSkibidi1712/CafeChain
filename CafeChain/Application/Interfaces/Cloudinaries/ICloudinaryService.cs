using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Common;
using CafeChain.Models.Enums.Cloudinaries;

namespace CafeChain.Application.Interfaces.Cloudinaries
{
    public interface ICloudinaryService
    {
        Task<UploadImageResult> UploadAsync(IFormFile file, string folder, ImageCategory category);

        Task<List<UploadImageResult>> UploadManyAsync(IEnumerable<IFormFile> files, string folder, ImageCategory category);

        Task<bool> DeleteAsync(string publicId);
    }
}
