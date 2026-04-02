using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Data; // Chỉnh lại theo tên DbContext của bác
using CafeChain.Models.Customers;
using CafeChain.ViewModels.Customers;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Customers
{
    public class CustomerService : ICustomerService
    {
        private readonly AppDbContext _context;
        // 1. Khai báo biến _fileService
        private readonly IFileService _fileService;

        // 2. Tiêm IFileService vào Constructor
        public CustomerService(AppDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        public async Task<CustomerProfileViewModel> GetCustomerProfileAsync(string accountId)
        {
            // Ép kiểu accountId từ chuỗi (lấy từ Claim) sang số (int)
            if (!int.TryParse(accountId, out int accId))
            {
                return null;
            }

            // Truy xuất Account kèm Customer và các thông tin phụ
            var account = await _context.Accounts // Thay bằng bảng Account tương ứng
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerAddresses)
                        .ThenInclude(ca => ca.Ward)         // 🔥 UPDATE: Kéo Phường
                            .ThenInclude(w => w.Province)   // 🔥 UPDATE: Kéo Tỉnh
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerPhones)
                .FirstOrDefaultAsync(a => a.AccountId == accId);

            if (account == null || account.Customer == null)
            {
                return null;
            }

            // Map dữ liệu sang ViewModel
            return new CustomerProfileViewModel
            {
                Customer = account.Customer,
                Email = account.Email,
                // Giả sử SĐT lúc đăng ký được lưu trong bảng Account (hoặc tùy cấu trúc bác)
                // PhoneNumber = account.PhoneNumber 
            };

        }
        public async Task<(string Url, bool IsReused)> UpdateAvatarAsync(int customerId, IFormFile file)
        {
            // Gọi FileService để xử lý file vật lý
            var fileResult = await _fileService.SaveImageWithHashAsync(file, "avatars");

            // Cập nhật Database
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer != null)
            {
                customer.AvatarUrl = fileResult.Url;
                await _context.SaveChangesAsync();
            }

            // Bóc tách cái Class ra để trả về đúng định dạng Tuple mà hàm đang yêu cầu
            return (fileResult.Url, fileResult.IsReused);
        }
        public async Task<bool> UpdateProfileAsync(int customerId, UpdateProfileRequest request)
        {
            var customer = await _context.Customers
                .Include(c => c.CustomerPhones)
                .Include(c => c.CustomerAddresses)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null) return false;

            // 1. Cập nhật thông tin cơ bản
            customer.FullName = request.FullName;
            customer.DateOfBirth = request.Dob;

            // 2. Thêm Số điện thoại mới
            if (request.NewPhones != null && request.NewPhones.Any())
            {
                foreach (var p in request.NewPhones)
                {
                    if (!customer.CustomerPhones.Any(cp => cp.Phone == p))
                    {
                        customer.CustomerPhones.Add(new CustomerPhone { Phone = p });
                    }
                }
            }

            // [NEW MAGIC] 2.5 Cập nhật Địa chỉ cũ (Khách sửa địa chỉ)
            if (request.UpdatedAddresses != null && request.UpdatedAddresses.Any())
            {
                foreach (var updateDto in request.UpdatedAddresses)
                {
                    var existing = customer.CustomerAddresses.FirstOrDefault(a => a.CustomerAddressId == updateDto.CustomerAddressId);
                    if (existing != null)
                    {
                        existing.Address = updateDto.Street;
                        existing.WardId = updateDto.WardId;
                    }
                }
            }

            // 3. Thêm Địa chỉ mới (Logic CHUYÊN GIA: Không N+1, không dư thừa)
            var newlyAddedAddresses = new List<(CustomerAddress Entity, int TempId)>();

            if (request.NewAddresses != null && request.NewAddresses.Any())
            {
                foreach (var addrDto in request.NewAddresses)
                {
                    // 🔥 Không query Wards! Entity Framework tự gán FK bằng WardId
                    var newAddr = new CustomerAddress 
                    { 
                        Address = addrDto.Street, // CHỈ lưu Số nhà/Đường
                        WardId = addrDto.WardId   // Lưu khóa ngoại
                    };
                    customer.CustomerAddresses.Add(newAddr);
                    newlyAddedAddresses.Add((newAddr, addrDto.TempId)); // Nhớ lại TempId để lật cờ Mặc định nếu cần
                }
            }

            // =====================================================================
            // 🔥 CHÈN LOGIC LẬT CỜ ISDEFAULT VÀO ĐÚNG ĐÂY 🔥
            // =====================================================================

            // XỬ LÝ SỐ ĐIỆN THOẠI MẶC ĐỊNH
            if (!string.IsNullOrEmpty(request.PrimaryPhone))
            {
                // Duyệt qua tất cả các số (bao gồm cả số cũ lẫn số MỚI vừa được thêm ở bước 2)
                foreach (var p in customer.CustomerPhones)
                {
                    p.IsDefault = (p.Phone == request.PrimaryPhone);
                }
            }

            // 🔥 XỬ LÝ ĐỊA CHỈ MẶC ĐỊNH BẰNG ID THAY VÌ CHUỖI
            if (request.PrimaryAddressId.HasValue)
            {
                int pId = request.PrimaryAddressId.Value;

                if (pId < 0) 
                {
                    // Nếu ID âm (< 0), nghĩa là khách hàng chọn 1 địa chỉ VỪA THÊM làm mặc định
                    foreach (var a in customer.CustomerAddresses) { a.IsDefault = false; }
                    var target = newlyAddedAddresses.FirstOrDefault(x => x.TempId == pId).Entity;
                    if (target != null) target.IsDefault = true;
                } 
                else 
                {
                    // Cập nhật địa chỉ cũ làm mặc định
                    foreach (var a in customer.CustomerAddresses)
                    {
                        a.IsDefault = (a.CustomerAddressId == pId);
                    }
                }
            }
            // =====================================================================

            // KHÔNG CỐ TÌNH SẮP XẾP HAY XÓA GÌ Ở ĐÂY CẢ
            return await _context.SaveChangesAsync() > 0;
        }
        public async Task<(bool Success, string Message)> ChangePasswordAsync(int accountId, ChangePasswordViewModel request)
        {
            // 1. Tìm tài khoản trong Database
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
            if (account == null)
            {
                return (false, "Không tìm thấy tài khoản.");
            }

            // 2. Kiểm tra mật khẩu hiện tại bằng BCrypt (Đồng bộ với logic Login)
            bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash);

            if (!isCurrentPasswordValid)
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            // ====================================================================
            // 🔥 THÊM LOGIC NÀY: CHẶN TRÙNG MẬT KHẨU CŨ
            // ====================================================================
            if (request.CurrentPassword == request.NewPassword)
            {
                return (false, "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
            }

            // 3. Mã hóa mật khẩu mới bằng BCrypt (Đồng bộ với logic Register)
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            // 4. Lưu thay đổi
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();

            return (true, "Đổi mật khẩu thành công!");
        }

        public async Task<CafeChain.Models.Customers.Customer> GetByPhoneAsync(string phone)
        {
            var customerPhone = await _context.CustomerPhones
                .Include(p => p.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Phone == phone);

            return customerPhone?.Customer;
        }

        public async Task<(bool Success, string Message, int CustomerId)> QuickRegisterAsync(string fullName, string phone)
        {
            var exists = await _context.CustomerPhones.AnyAsync(p => p.Phone == phone);
            if (exists) return (false, "Số điện thoại này đã được sử dụng.", 0);

            var account = new Account
            {
                Email = $"pos_{phone}@cafechain.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Active = true,
                CreatedAt = DateTime.Now
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var customer = new Customer
            {
                AccountId = account.AccountId,
                FullName = fullName,
                CreatedAt = DateTime.Now,
                Active = true
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var cp = new CustomerPhone
            {
                CustomerId = customer.CustomerId,
                Phone = phone,
                IsDefault = true
            };
            _context.CustomerPhones.Add(cp);

            var initialPoint = new CustomerPoint
            {
                CustomerId = customer.CustomerId,
                Points = 0
            };
            _context.CustomerPoints.Add(initialPoint);

            await _context.SaveChangesAsync();

            return (true, "Đăng ký thành viên thành công!", customer.CustomerId);
        }

        // ======================= LOCATION METHODS =========================
        public async Task<List<CafeChain.Models.Locations.Province>> GetProvincesAsync()
        {
            return await _context.Provinces.ToListAsync();
        }

        public async Task<List<CafeChain.Models.Locations.Ward>> GetWardsByProvinceAsync(int provinceId)
        {
            return await _context.Wards
                .Where(w => w.ProvinceId == provinceId)
                .ToListAsync();
        }
    }
}