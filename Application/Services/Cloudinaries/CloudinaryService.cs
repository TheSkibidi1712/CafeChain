using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Models.Enums.Cloudinaries;
using CafeChain.Application.DTOs.Common;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Helpers.Cloudinaries;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
namespace CafeChain.Application.Services.Cloudinaries
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary; 
        private readonly ILogger<CloudinaryService> _logger; 

        public CloudinaryService(Cloudinary cloudinary, ILogger<CloudinaryService> logger) 
        {
            _cloudinary = cloudinary; 
            _logger = logger; 
        }

        public async Task<UploadImageResult> UploadAsync(IFormFile file, string folder, ImageCategory category)
        {
            ImageValidationHelper.Validate(file, category);

            var publicId = GeneratePublicId(category);

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(
                    file.FileName,
                    stream),

                Folder = folder,

                PublicId = publicId,

                Overwrite = false,

                Transformation = BuildTransformation(category)
            };

            var result = await _cloudinary
                .UploadAsync(uploadParams);

            if (result.Error != null)
            {
                _logger.LogError(
                    "Cloudinary upload failed: {Message}",
                    result.Error.Message);

                throw new Exception(
                    $"Upload image failed: {result.Error.Message}");
            }

            return new UploadImageResult
            {
                Url = result.SecureUrl.ToString(),
                PublicId = result.PublicId,
                Bytes = result.Bytes,
                Format = result.Format
            };
        }

        public async Task<List<UploadImageResult>> UploadManyAsync(IEnumerable<IFormFile> files, string folder, ImageCategory category)
        {
            var results = new List<UploadImageResult>();

            foreach (var file in files)
            {
                var uploadResult = await UploadAsync(
                    file,
                    folder,
                    category);

                results.Add(uploadResult);
            }

            return results;
        }

        public async Task<bool> DeleteAsync(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return false;

            var deleteParams =
                new DeletionParams(publicId);

            var result =
                await _cloudinary.DestroyAsync(deleteParams);

            return result.Result == "ok";
        }

        // ============================================= Private Helpers =============================================

        private static string GeneratePublicId(ImageCategory category)
        {
            return $"{category.ToString().ToLower()}_{Guid.NewGuid():N}";
        }

        private static Transformation BuildTransformation(ImageCategory category)
        {
            switch (category)
            {
                case ImageCategory.Avatar:
                    return new Transformation()
                        .Width(500)
                        .Height(500)
                        .Crop("fill")
                        .Quality("auto")
                        .FetchFormat("auto");

                case ImageCategory.Drink:
                    return new Transformation()
                        .Width(1000)
                        .Height(1000)
                        .Crop("fill")
                        .Quality("auto")
                        .FetchFormat("auto");

                case ImageCategory.Topping:
                    return new Transformation()
                        .Width(800)
                        .Height(800)
                        .Crop("fill")
                        .Quality("auto")
                        .FetchFormat("auto");

                case ImageCategory.Rating:
                    return new Transformation()
                        .Width(1200)
                        .Height(1200)
                        .Crop("limit")
                        .Quality("auto")
                        .FetchFormat("auto");

                default:
                    return new Transformation()
                        .Quality("auto")
                        .FetchFormat("auto");
            }
        }
    }
}
