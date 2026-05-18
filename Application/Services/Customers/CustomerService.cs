using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Data; // Chỉnh lại theo tên DbContext của bác
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Customer;
using CafeChain.Models.Loyalties;
using CafeChain.ViewModels.Customers;
using Castle.Core.Resource;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
namespace CafeChain.Application.Services.Customers
{
    public class CustomerService : ICustomerService
    {
        private readonly AppDbContext _context;
        private readonly IFileService _fileService;
        private readonly IGeocodingService _geocodingService;

        public CustomerService(AppDbContext context, IFileService fileService, IGeocodingService geocodingService)
        {
            _context = context;
            _fileService = fileService;
            _geocodingService = geocodingService;
        }

        public async Task<CustomerProfileViewModel?> GetCustomerProfileAsync(string accountId)
        {
            // Parse accountId từ Claim
            if (!int.TryParse(accountId, out int accId))
            {
                return null;
            }

            // =========================
            // LOAD ACCOUNT + CUSTOMER
            // =========================

            var account = await _context.Accounts
                .Include(a => a.Customer)
                    .ThenInclude(c => c.MemberLevel)
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerAddresses)
                        .ThenInclude(ca => ca.Ward)
                            .ThenInclude(w => w.District)
                                .ThenInclude(d => d.Province)
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerPhones)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accId);

            if (account == null || account.Customer == null)
            {
                return null;
            }

            var customer = account.Customer;

            // =========================
            // CURRENT POINTS
            // =========================

            int totalPoints = customer.CurrentPoints;

            // =========================
            // MEMBER LEVEL
            // =========================

            var currentTier = customer.MemberLevel;

            string currentTierName = currentTier?.Name ?? "Thành viên mới";

            // =========================
            // NEXT TIER
            // =========================

            MemberLevel? nextTier = null;

            if (currentTier != null)
            {
                nextTier = await _context.MemberLevels
                    .Where(x => x.MinPoints > currentTier.MinPoints)
                    .OrderBy(x => x.MinPoints)
                    .FirstOrDefaultAsync();
            }
            else
            {
                nextTier = await _context.MemberLevels
                    .OrderBy(x => x.MinPoints)
                    .FirstOrDefaultAsync();
            }

            string nextTierName = string.Empty;

            int pointsNeeded = 0;

            double progressPercentage = 100;

            if (nextTier != null)
            {
                nextTierName = nextTier.Name;

                int currentMin = currentTier?.MinPoints ?? 0;

                int nextMin = nextTier.MinPoints;

                pointsNeeded = Math.Max(0, nextMin - totalPoints);

                progressPercentage =
                    (double)(totalPoints - currentMin)
                    / (nextMin - currentMin) * 100;

                progressPercentage = Math.Clamp(progressPercentage, 0, 100);
            }

            // =========================
            // MAP VIEWMODEL
            // =========================

            return new CustomerProfileViewModel
            {
                Customer = customer,

                Email = account.Email,

                TotalPoints = totalPoints,

                CurrentTierName = currentTierName,

                NextTierName = nextTierName,

                PointsNeeded = pointsNeeded,

                ProgressPercentage = progressPercentage
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
                        existing.DistrictId = updateDto.DistrictId;
                        existing.ProvinceId = updateDto.ProvinceId;

                        var wardName = await _context.Wards.Where(w => w.WardId == updateDto.WardId).Select(w => w.Name).FirstOrDefaultAsync();
                        var districtName = await _context.Districts.Where(d => d.DistrictId == updateDto.DistrictId).Select(d => d.Name).FirstOrDefaultAsync();
                        var provinceName = await _context.Provinces.Where(p => p.ProvinceId == updateDto.ProvinceId).Select(p => p.Name).FirstOrDefaultAsync();

                        // 1. Phân rã trực tiếp kết quả trả về thành 2 biến lat và lng
                        var (lat, lng) = await _geocodingService.GetCoordinatesAsync($"...");

                        // 2. Kiểm tra xem 2 biến này có chứa dữ liệu thật không
                        if (lat != null && lng != null)
                        {
                            existing.Latitude = lat;
                            existing.Longitude = lng;
                        }
                    }
                }
            }

            // 3. Thêm Địa chỉ mới (Logic CHUYÊN GIA: Không N+1, không dư thừa)
            var newlyAddedAddresses = new List<(CustomerAddress Entity, int TempId)>();

            if (request.NewAddresses != null && request.NewAddresses.Any())
            {
                foreach (var addrDto in request.NewAddresses)
                {
                    var newAddr = new CustomerAddress 
                    { 
                        Address = addrDto.Street, // CHỈ lưu Số nhà/Đường
                        WardId = addrDto.WardId,  // Lưu khóa ngoại Phường
                        DistrictId = addrDto.DistrictId, // Lưu khóa ngoại Quận
                        ProvinceId = addrDto.ProvinceId  // Lưu khóa ngoại Tỉnh
                    };

                    var wardName = await _context.Wards.Where(w => w.WardId == addrDto.WardId).Select(w => w.Name).FirstOrDefaultAsync();
                    var districtName = await _context.Districts.Where(d => d.DistrictId == addrDto.DistrictId).Select(d => d.Name).FirstOrDefaultAsync();
                    var provinceName = await _context.Provinces.Where(p => p.ProvinceId == addrDto.ProvinceId).Select(p => p.Name).FirstOrDefaultAsync();

                    var (lat, lng) = await _geocodingService.GetCoordinatesAsync($"{addrDto.Street}, {wardName}, {districtName}, {provinceName}");
                    if (lat != null && lng != null)
                    {
                        newAddr.Latitude = lat;
                        newAddr.Longitude = lng;
                    }

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
            // =========================
            // CHECK DUPLICATE PHONE
            // =========================

            var exists = await _context.CustomerPhones
                .AnyAsync(p => p.Phone == phone);

            if (exists)
            {
                return (false, "Số điện thoại này đã được sử dụng.", 0);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // =========================
                // CREATE ACCOUNT
                // =========================

                var account = new Account
                {
                    Email = $"pos_{phone}@cafechain.com",

                    PasswordHash = BCrypt.Net.BCrypt
                        .HashPassword(Guid.NewGuid().ToString()),

                    Active = true,

                    CreatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);

                await _context.SaveChangesAsync();

                // =========================
                // CREATE CUSTOMER
                // =========================

                var customer = new Customer
                {
                    AccountId = account.AccountId,

                    CustomerCode = $"CUS{DateTime.UtcNow.Ticks}",

                    FullName = fullName,

                    Category = CustomerCategory.Registered,

                    CurrentPoints = 0,

                    TotalSpent = 0,

                    TotalOrders = 0,

                    Active = true,

                    CreatedAt = DateTime.UtcNow,

                    IsDeleted = false
                };

                _context.Customers.Add(customer);

                await _context.SaveChangesAsync();

                // =========================
                // CREATE PHONE
                // =========================

                var customerPhone = new CustomerPhone
                {
                    CustomerId = customer.CustomerId,

                    Phone = phone,

                    IsDefault = true
                };

                _context.CustomerPhones.Add(customerPhone);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (
                    true,
                    "Đăng ký thành viên thành công!",
                    customer.CustomerId
                );
            }
            catch
            {
                await transaction.RollbackAsync();

                return (
                    false,
                    "Đăng ký thành viên thất bại.",
                    0
                );
            }
        }


        // ======================= LOCATION METHODS =========================
        public async Task<List<CafeChain.Models.Locations.Province>> GetProvincesAsync()
        {
            return await _context.Provinces.ToListAsync();
        }

        public async Task<List<CafeChain.Models.Locations.District>> GetDistrictsByProvinceAsync(int provinceId)
        {
            return await _context.Districts
                .Where(d => d.ProvinceId == provinceId)
                .ToListAsync();
        }

        public async Task<List<CafeChain.Models.Locations.Ward>> GetWardsByDistrictAsync(int districtId)
        {
            return await _context.Wards
                .Where(w => w.DistrictId == districtId)
                .ToListAsync();
        }
    }
}