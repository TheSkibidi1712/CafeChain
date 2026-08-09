using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Customer
{
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Họ và tên không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên phải từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[\p{L}\s]+$", ErrorMessage = "Họ và tên chỉ được chứa chữ cái và khoảng trắng.")]
        public string FullName { get; set; } = null!;

        public DateTime? Dob { get; set; }

        public string? PrimaryPhone { get; set; }

        public int? PrimaryAddressId { get; set; }

        public List<string> NewPhones { get; set; } = new();

        public List<NewAddressDto> NewAddresses { get; set; } = new();

        public List<UpdateAddressDto> UpdatedAddresses { get; set; } = new();
    }

    public class NewAddressDto
    {
        public int TempId { get; set; }

        [Required(ErrorMessage = "Địa chỉ không được để trống.")]
        [StringLength(255)]
        public string Street { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int WardId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProvinceId { get; set; }
    }

    public class UpdateAddressDto
    {
        [Range(1, int.MaxValue)]
        public int CustomerAddressId { get; set; }

        [Required(ErrorMessage = "Địa chỉ không được để trống.")]
        [StringLength(255)]
        public string Street { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int WardId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProvinceId { get; set; }
    }
}
