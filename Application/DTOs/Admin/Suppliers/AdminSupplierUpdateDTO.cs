using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    public class AdminSupplierUpdateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Mã nhà cung cấp không được để trống.")]
        [RegularExpression(@"^SUP[0-9]{3,10}$", ErrorMessage = "Mã NCC phải theo định dạng SUP##### (VD: SUP001, SUP00001).")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Tên nhà cung cấp không được để trống.")]
        [StringLength(150, ErrorMessage = "Tên nhà cung cấp tối đa 150 ký tự.")]
        public string Name { get; set; }

        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Số điện thoại phải gồm 10 hoặc 11 chữ số.")]
        public string Phone { get; set; }

        [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự.")]
        public string Address { get; set; }

        public bool Active { get; set; }
    }
}
