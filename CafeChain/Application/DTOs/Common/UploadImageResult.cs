namespace CafeChain.Application.DTOs.Common
{
    public class UploadImageResult
    {
        public string Url { get; set; } = null!; 
        
        public string PublicId { get; set; } = null!; 
        
        public long Bytes { get; set; }

        public string Format { get; set; } = null!;

    }
}
