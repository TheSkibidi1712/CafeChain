using CafeChain.Models.Enums.Cloudinaries;

namespace CafeChain.Helpers.Cloudinaries
{
    public static class ImageValidationHelper
    {
        private static readonly string[] AllowedExtensions =  { ".jpg", ".jpeg", ".png", ".webp" };
        
        private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp", "image/jpg" };

        public static void Validate(IFormFile file, ImageCategory category)
        {
            if (file == null)
            {
                throw new Exception("Image file is required.");
            }

            if (file.Length <= 0)
            {
                throw new Exception("Image file is empty.");
            }

            ValidateExtension(file); 

            ValidateMimeType(file); 
            
            ValidateFileSize(file, category); 
        }
        private static void ValidateExtension(IFormFile file) 
        { 
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant(); 

            if (!AllowedExtensions.Contains(extension)) 
            { 
                throw new Exception("Only .jpg, .jpeg, .png, .webp are allowed."); 
            } 
        }

        private static void ValidateMimeType(IFormFile file) 
        { 
            if (!AllowedMimeTypes.Contains(file.ContentType)) 
            { 
                throw new Exception("Invalid image mime type."); 
            } 
        }

        private static void ValidateFileSize(IFormFile file, ImageCategory category) 
        { 
            long maxSize = category 
            switch 
            { 
                ImageCategory.Avatar => 2 * 1024 * 1024, 
                ImageCategory.Drink => 5 * 1024 * 1024, 
                ImageCategory.Topping => 3 * 1024 * 1024, 
                ImageCategory.Rating => 5 * 1024 * 1024, 
                _ => 5 * 1024 * 1024 
            }; 

            if (file.Length > maxSize) 
            { 
                throw new Exception($"Image exceeds max size of {maxSize / 1024 / 1024}MB."); 
            } 
        }
    }
}
