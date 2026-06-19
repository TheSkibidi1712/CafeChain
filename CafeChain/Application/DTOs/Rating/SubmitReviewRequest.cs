using Microsoft.AspNetCore.Http;

// Đổi namespace cho khớp với cấu trúc thư mục thực tế của bác nhé
namespace CafeChain.Application.DTOs.Drinks
{
    public class SubmitReviewRequest
    {
        public int DrinkId { get; set; }
        public int Stars { get; set; }
        public string Comment { get; set; }
        public IFormFileCollection Images { get; set; }
    }
}