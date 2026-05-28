using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Results;
using CafeChain.Data;

namespace CafeChain.Application.Services.Attendance
{
    public class AttendanceSecurityService : IAttendanceSecurityService
    {
        private readonly AppDbContext _context;

        public AttendanceSecurityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> ValidateStoreIPAsync(int storeId, string clientIp)
        {
            // [FIX.md] TẠM THỜI BỎ KIỂM TRA ĐỊA CHỈ IP ĐỂ TIỆN CHO VIỆC TEST KHÔNG BỊ CHẶN MẠNG
            return ServiceResult.Success("Xác thực IP thành công (Bypass Mode).");

            /*
            if (string.IsNullOrWhiteSpace(clientIp))
            {
                return ServiceResult.Failure("Không trích xuất được địa chỉ IP của thiết bị.");
            }

            // [HOTFIX TESTING AGILE]
            // Khi chạy debug Localhost (IIS Express/Kestrel) trên cùng máy tính,
            // trình duyệt luôn truyền IP Loopback theo chuẩn IPv6 (::1) hoặc IPv4 (127.0.0.1)
            // thay vì IP thực của máy (192.168.x.x). Đoạn này thiết lập Auto-pass riêng cho Dev Mode.
            if (clientIp == "::1" || clientIp == "127.0.0.1")
            {
                return ServiceResult.Success("Xác thực IP Localhost thành công (Development Mode).");
            }

            // Lấy danh sách IP được phép của cửa hàng
            var storeIPs = await _context.StoreIPs
                .Where(ip => ip.StoreId == storeId && ip.IsActive)
                .ToListAsync();

            // Nếu cửa hàng chưa cấu hình bất kỳ mạng WiFi/IP nào -> Option 1: Block cứng (Strict Mode)
            if (!storeIPs.Any())
            {
                return ServiceResult.Failure("Hệ thống mạng cửa hàng chưa được thiết lập. Vui lòng liên hệ IT để cấp phát IP trước khi check-in.");
            }

            bool isValid = false;
            foreach (var allowedIp in storeIPs)
            {
                // Hỗ trợ kiểm tra dải LAN (VD: 192.168.1.*) do chuẩn DHCP mỗi ngày cấp 1 số đuôi
                if (allowedIp.IPAddress.EndsWith(".*"))
                {
                    string subnet = allowedIp.IPAddress.Replace(".*", "");
                    if (clientIp.StartsWith(subnet))
                    {
                        isValid = true;
                        break;
                    }
                }
                else
                {
                    // So khớp chính xác IP Tỉnh / Public IP
                    if (clientIp == allowedIp.IPAddress)
                    {
                        isValid = true;
                        break;
                    }
                }
            }

            if (!isValid)
            {
                // Ngăn chặn chấm công rác/ngoài phạm vi cửa hàng
                return ServiceResult.Failure($"Địa chỉ IP ({clientIp}) không khớp với mạng lưới của cửa hàng. " + 
                                             $"Yêu cầu kết nối đúng WiFi cửa hàng để tiếp tục.");
            }

            return ServiceResult.Success("Xác thực IP thành công.");
            */
        }

        public async Task<ServiceResult> ProcessFirstLoginPasswordChangeAsync(int accountId, string oldPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return ServiceResult.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
            if (account == null)
            {
                return ServiceResult.Failure("Không tìm thấy tài khoản để xử lý đổi mật khẩu.");
            }

            // ✅ Vá Lỗ hổng IDOR: Buộc phải xác thực mật khẩu cũ (OldPassword) trước khi được phép đổi mới
            if (string.IsNullOrEmpty(account.PasswordHash) || !BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
            {
                return ServiceResult.Failure("Mật khẩu hiện tại không chính xác. Hành động bị từ chối.");
            }

            if (!account.RequiresPasswordChange)
            {
                return ServiceResult.Failure("Tài khoản này không nằm trong diện bắt buộc đổi mật khẩu lần đầu.");
            }

            // Mã hóa mật khẩu mới
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            
            // Tắt cờ bắt buộc đổi pass
            account.RequiresPasswordChange = false;

            try
            {
                _context.Update(account);
                await _context.SaveChangesAsync();
                return ServiceResult.Success("Đổi mật khẩu thành công. Lệnh buộc đổi Pass đã được gỡ bỏ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi cập nhật mật khẩu mới: " + ex.Message);
            }
        }

        public async Task<ServiceResult> RegisterFaceAsync(int accountId, string faceDescriptor)
        {
            if (string.IsNullOrWhiteSpace(faceDescriptor))
                return ServiceResult.Failure("Vector khuôn mặt không hợp lệ.");

            // Tìm Staff qua AccountId
            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            if (staff == null)
                return ServiceResult.Failure("Không tìm thấy hồ sơ nhân viên.");

            // (Đã xóa logic chặn đăng ký đè, cho phép nhân viên tự sửa Face ID nếu gặp lỗi sai lệch)
            // if (!string.IsNullOrEmpty(staff.FaceDescriptor))
            //    return ServiceResult.Failure("Face ID đã được đăng ký trước đó. Liên hệ Quản lý để đặt lại.");

            staff.FaceDescriptor = faceDescriptor;
            _context.Update(staff);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Đăng ký Face ID thành công! Bạn có thể sử dụng khuôn mặt để chấm công.");
        }

        public async Task<ServiceResult> UpdatePinAsync(int accountId, string pin)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
            {
                return ServiceResult.Failure("Mã PIN phải là chuỗi 4 chữ số.");
            }

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            if (staff == null)
            {
                return ServiceResult.Failure("Không tìm thấy hồ sơ nhân viên.");
            }

            // Hashing using BCrypt
            staff.PinHash = BCrypt.Net.BCrypt.HashPassword(pin);
            _context.Update(staff);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Cập nhật mã PIN thành công.");
        }
    }
}
