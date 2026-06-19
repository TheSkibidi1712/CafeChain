using CafeChain.Application.Services;

namespace CafeChain.Application.Interfaces
{
    public interface IFileService
    {
        Task<FileUploadResult> SaveImageWithHashAsync(IFormFile file, string folderName);


        //// Trả về một Object chứa URL và trạng thái IsReused
        //Task<(string Url, bool IsReused)> SaveImageWithHashAsync(IFormFile file, string folderName);
    }
}
