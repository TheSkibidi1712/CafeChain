using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Results;
using CafeChain.Data;
using System.Text.Json;
using CafeChain.Models.Staffs;

namespace CafeChain.Application.Services.Attendance
{
    public class AttendanceActionService : IAttendanceActionService
    {
        private readonly AppDbContext _context;

        public AttendanceActionService(AppDbContext context)
        {
            _context = context;
        }

        private float CalculateEuclideanDistance(float[] source, float[] target)
        {
            if (source == null || target == null || source.Length != target.Length || source.Length == 0)
                return float.MaxValue;
            float sum = 0;
            for (int i = 0; i < source.Length; i++)
            {
                float diff = source[i] - target[i];
                sum += diff * diff;
            }
            return (float)Math.Sqrt(sum);
        }

        public async Task<ServiceResult> SubmitTimeActionAsync(int accountId, string actionType, string faceDescriptor)
        {
            if (string.IsNullOrEmpty(actionType))
                return ServiceResult.Failure("Loại hành động không hợp lệ");

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            if (staff == null)
                return ServiceResult.Failure("Không tìm thấy thông tin nhân viên");

            // 🔥 BƯỚC 1: XÁC THỰC VECTOR KHUÔN MẶT C# SERVER-SIDE (Zero Trust Client)
            if (string.IsNullOrEmpty(staff.FaceDescriptor))
                return ServiceResult.Failure("Nhân viên chưa được đăng ký Dữ liệu Sinh trắc học khuôn mặt. Vui lòng liên hệ Quản lý.");

            if (string.IsNullOrEmpty(faceDescriptor))
                return ServiceResult.Failure("Thiết bị không gửi Vector khuôn mặt hợp lệ.");

            try
            {
                var serverVector = JsonSerializer.Deserialize<float[]>(staff.FaceDescriptor);
                var clientVector = JsonSerializer.Deserialize<float[]>(faceDescriptor);

                float distance = CalculateEuclideanDistance(serverVector, clientVector);
                if (distance > 0.6f) // Ngưỡng face-api tiêu chuẩn 0.6
                {
                    return ServiceResult.Failure($"Xác thực thất bại! Khuôn mặt không khớp. (Distance: {distance:F2})");
                }
            }
            catch
            {
                return ServiceResult.Failure("Dữ liệu Vector sinh trắc học bị sai định dạng.");
            }

            // 🔥 BƯỚC 2: ROW-LEVEL LOCK TRANSACTION QUẢN LÝ RACE CONDITION
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var today = DateTime.Today;
                var todayDateStr = today.ToString("yyyy-MM-dd");

                // Dùng UPDLOCK khóa record đến khi commit, chống SPAM click liên tiếp sinh ra Exception ghi đè
                var shiftsToday = await _context.StaffShifts
                    .FromSqlInterpolated($"SELECT * FROM StaffShifts WITH (UPDLOCK, ROWLOCK) WHERE StaffId = {staff.StaffId} AND CAST(WorkDate as Date) = {todayDateStr}")
                    .Include(s => s.Shift)
                    .ToListAsync();

                var todayShifts = shiftsToday.OrderBy(s => s.Shift?.StartTime).ToList();
                StaffShift todayShift = null;

                if (actionType == "CheckIn")
                {
                    todayShift = todayShifts.FirstOrDefault(s => !s.ActualCheckIn.HasValue);
                }
                else
                {
                    todayShift = todayShifts.FirstOrDefault(s => s.ActualCheckIn.HasValue && !s.ActualCheckOut.HasValue);
                }

                if (todayShift == null)
                    return ServiceResult.Failure("Không tìm thấy ca làm việc phù hợp cho hành động này (Có thể bạn chưa được xếp ca hoặc đã hoàn thành lịch hôm nay)!");

                var currentTime = DateTime.Now;

                switch (actionType)
                {
                    case "CheckIn":
                        if (todayShift.ActualCheckIn.HasValue) return ServiceResult.Failure("Bạn đã Vào Ca rồi!");
                        todayShift.ActualCheckIn = currentTime;
                        todayShift.StatusId = 2; // In Progress
                        break;
                    case "CheckOut":
                        if (!todayShift.ActualCheckIn.HasValue) return ServiceResult.Failure("Bạn phải Vào Ca trước khi Tan Ca!");
                        todayShift.ActualCheckOut = currentTime;
                        todayShift.StatusId = 3; // Completed
                        break;
                    case "StartBreak":
                        if (!todayShift.ActualCheckIn.HasValue) return ServiceResult.Failure("Chưa Vào Ca, không thể nghỉ!");
                        todayShift.StatusId = 4; // On Break
                        break;
                    case "EndBreak":
                        todayShift.StatusId = 2; // Trở lại In Progress
                        break;
                    default:
                        return ServiceResult.Failure("Hành động không được hỗ trợ");
                }

                _context.Update(todayShift);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();

                return ServiceResult.Success($"Thực hiện '{actionType}' thành công lúc {currentTime:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure("Lỗi hệ thống lưu trữ: " + ex.Message);
            }
        }

        /// <summary>
        /// Trả về toàn bộ dữ liệu cần thiết cho màn hình Kiosk:
        /// - Thông tin nhân viên + cửa hàng
        /// - Trạng thái Face ID (đã đăng ký chưa)
        /// - Danh sách ca làm việc hôm nay kèm trạng thái
        /// </summary>
        public async Task<ServiceResult<object>> GetKioskDataAsync(int accountId)
        {
            try
            {
                // 1. Tìm nhân viên
                var staff = await _context.Staffs
                    .Include(s => s.Store)
                    .FirstOrDefaultAsync(s => s.AccountId == accountId);

                if (staff == null)
                    return ServiceResult<object>.Failure("Không tìm thấy hồ sơ nhân viên với tài khoản này.");

                // 2. Kiểm tra Face ID
                bool hasFaceId = !string.IsNullOrEmpty(staff.FaceDescriptor);

                // 3. Lấy lịch ca hôm nay
                var today = DateTime.Today;
                var shiftsToday = await _context.StaffShifts
                    .Where(ss => ss.StaffId == staff.StaffId && ss.WorkDate.Date == today)
                    .Include(ss => ss.Shift)
                    .Include(ss => ss.Status)
                    .OrderBy(ss => ss.Shift.StartTime)
                    .ToListAsync();

                // 4. Map sang DTO trả về Frontend
                var shiftDtos = shiftsToday.Select(ss =>
                {
                    var now = DateTime.Now.TimeOfDay;
                    
                    var actualStart = ss.CustomStartTime ?? ss.Shift?.StartTime ?? TimeSpan.Zero;
                    var actualEnd = ss.CustomEndTime ?? ss.Shift?.EndTime ?? TimeSpan.Zero;
                    bool isNightShift = actualEnd <= actualStart;
                    bool isCurrent = false;

                    if (!isNightShift)
                    {
                        isCurrent = actualStart <= now && actualEnd >= now;
                    }
                    else // Ca qua đêm
                    {
                        isCurrent = now >= actualStart || now <= actualEnd;
                    }

                    string status;
                    if (ss.StatusId == 3) // Completed
                        status = "completed";
                    else if (ss.ActualCheckIn.HasValue && ss.StatusId != 3)
                        status = "current";
                    else if (isCurrent && !ss.ActualCheckIn.HasValue)
                        status = "current";
                    else if (!isNightShift && actualStart > now || isNightShift && now < actualStart && now > actualEnd)
                        status = "upcoming";
                    else
                        status = "upcoming";

                    return new
                    {
                        shiftName = ss.Shift?.Name ?? "N/A",
                        startTime = ss.Shift != null ? ss.Shift.StartTime.ToString(@"hh\:mm") : "00:00",
                        endTime = ss.Shift != null ? ss.Shift.EndTime.ToString(@"hh\:mm") : "00:00",
                        statusCode = ss.Status?.Code ?? "PLANNED",
                        statusName = ss.Status?.Name ?? "Planned",
                        uiStatus = status,
                        actualCheckIn = ss.ActualCheckIn.HasValue ? ss.ActualCheckIn.Value.ToString("HH:mm") : null,
                        actualCheckOut = ss.ActualCheckOut.HasValue ? ss.ActualCheckOut.Value.ToString("HH:mm") : null,
                        hours = ss.Shift != null ? (ss.Shift.EndTime - ss.Shift.StartTime).TotalHours.ToString("F1") : "0"
                    };
                }).ToList();

                // 5. Tổng hợp kết quả
                var kioskData = new
                {
                    staffName = staff.FullName,
                    staffId = staff.StaffId,
                    storeName = staff.Store?.Name ?? "N/A",
                    storeAddress = staff.Store?.Address ?? "Hệ thống CafeChain",
                    hasFaceId = hasFaceId,
                    shifts = shiftDtos,
                    checkInTime = shiftsToday.FirstOrDefault(s => s.ActualCheckIn.HasValue)?.ActualCheckIn?.ToString("HH:mm"),
                    totalShifts = shiftDtos.Count
                };

                return ServiceResult<object>.Success(kioskData);
            }
            catch (Exception ex)
            {
                return ServiceResult<object>.Failure("Lỗi xử lý Server: " + ex.Message);
            }
        }
    }
}
