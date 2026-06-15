using CafeChain.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CafeChain.Application.Services
{
    public class FileService : IFileService
    {
        // 1. GỌI BẢO BỐI IWebHostEnvironment
        private readonly IWebHostEnvironment _env;

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<FileUploadResult> SaveImageWithHashAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0) return new FileUploadResult { Url = null };

            // 2. KHÓA TỌA ĐỘ BẰNG _env.WebRootPath (Đây là Tọa độ Gốc tuyệt đối không bao giờ bị xóa)
            // Path sẽ ra dạng: C:\Projects\CafeChain\wwwroot\Images\avatars
            string uploadsFolder = Path.Combine(_env.WebRootPath, "Images", folderName);

            // Nếu thư mục chưa có thì tự động tạo
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // 3. TẠO TÊN FILE DUY NHẤT (Chống trùng lặp)
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 4. LƯU FILE VẬT LÝ VÀO Ổ CỨNG
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 5. TRẢ VỀ ĐƯỜNG DẪN ẢNH ĐỂ LƯU VÀO DATABASE
            return new FileUploadResult
            {
                Url = $"/Images/{folderName}/{uniqueFileName}",
                IsReused = false
            };
        }
    }

    // Class phụ trợ hứng kết quả
    public class FileUploadResult
    {
        public string Url { get; set; }
        public bool IsReused { get; set; }
    }
}