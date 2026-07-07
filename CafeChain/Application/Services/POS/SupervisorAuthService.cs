using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// Service xử lý nghiệp vụ Supervisor PIN Auth
    /// Inject ISupervisorRepository thay vì AppDbContext — đúng pattern Repository
    /// </summary>
    public class SupervisorAuthService : ISupervisorAuthService
    {
        private readonly ISupervisorRepository _repository;
        private readonly IMemoryCache _cache;

        private const int MAX_ATTEMPTS = 5;
        private const int LOCKOUT_MINUTES = 15;
        private const string CACHE_KEY_PREFIX = "PinAttempt_Store_";

        public SupervisorAuthService(ISupervisorRepository repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<ServiceResult> AuthorizePinAsync(string pin, int cashierId, int storeId, string actionName, int targetId, string reason, decimal? discountValue = null)
        {
            var supervisorResult = await FindSupervisorByPinAsync(pin, storeId);
            if (!supervisorResult.IsSuccess)
                return ServiceResult.Failure(supervisorResult.Message);

            var matchedSupervisor = supervisorResult.Data!;

            // 4. Success — reset attempts & create audit log via Repository
            _cache.Remove($"{CACHE_KEY_PREFIX}{storeId}");

            await _repository.CreateAuditLogAsync(new InvoiceAuditLog
            {
                OrderId = targetId > 0 ? targetId : null,
                CashierId = cashierId,
                SupervisorId = matchedSupervisor.StaffId,
                ActionName = actionName,
                Reason = reason,
                DiscountValue = discountValue,
                CreatedAt = DateTime.Now
            });

            return ServiceResult.Success($"Trưởng ca {matchedSupervisor.FullName} đã xác nhận thành công.");
        }

        public async Task<ServiceResult<SupervisorPinAuthorizationDto>> VerifySupervisorPinAsync(string pin, int storeId)
        {
            var supervisorResult = await FindSupervisorByPinAsync(pin, storeId);
            if (!supervisorResult.IsSuccess)
                return ServiceResult<SupervisorPinAuthorizationDto>.Failure(supervisorResult.Message);

            var matchedSupervisor = supervisorResult.Data!;
            _cache.Remove($"{CACHE_KEY_PREFIX}{storeId}");

            return ServiceResult<SupervisorPinAuthorizationDto>.Success(new SupervisorPinAuthorizationDto
            {
                SupervisorStaffId = matchedSupervisor.StaffId,
                SupervisorName = matchedSupervisor.FullName
            }, $"Supervisor/manager {matchedSupervisor.FullName} đã xác nhận thành công.");
        }

        public Task<int> GetRemainingAttemptsAsync(int storeId)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{storeId}";
            var attemptData = _cache.Get<PinAttemptData>(cacheKey);

            if (attemptData == null) return Task.FromResult(MAX_ATTEMPTS);
            if (attemptData.IsLocked && attemptData.LockoutUntil > DateTime.Now) return Task.FromResult(0);

            return Task.FromResult(MAX_ATTEMPTS - attemptData.FailedCount);
        }

        private async Task<ServiceResult<Staff>> FindSupervisorByPinAsync(string pin, int storeId)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4)
                return ServiceResult<Staff>.Failure("Mã PIN phải có đúng 4 chữ số.");

            var cacheKey = $"{CACHE_KEY_PREFIX}{storeId}";
            var attemptData = _cache.Get<PinAttemptData>(cacheKey);

            if (attemptData != null && attemptData.IsLocked && attemptData.LockoutUntil > DateTime.Now)
            {
                var remaining = (int)(attemptData.LockoutUntil - DateTime.Now).TotalMinutes + 1;
                return ServiceResult<Staff>.Failure($"Đã bị khóa do nhập sai quá {MAX_ATTEMPTS} lần. Vui lòng thử lại sau {remaining} phút.");
            }

            if (attemptData != null && attemptData.IsLocked && attemptData.LockoutUntil <= DateTime.Now)
            {
                attemptData = null;
                _cache.Remove(cacheKey);
            }

            var supervisors = await _repository.GetSupervisorsWithPinAsync(storeId);

            if (!supervisors.Any())
                return ServiceResult<Staff>.Failure("Không tìm thấy Supervisor/manager nào có mã PIN tại cửa hàng này.");

            Staff? matchedSupervisor = null;
            foreach (var sup in supervisors)
            {
                try
                {
                    if (BCrypt.Net.BCrypt.Verify(pin, sup.PinHash))
                    {
                        matchedSupervisor = sup;
                        break;
                    }
                }
                catch
                {
                    // PIN hash lỗi thì bỏ qua staff đó, không expose chi tiết ra POS.
                }
            }

            if (matchedSupervisor != null)
                return ServiceResult<Staff>.Success(matchedSupervisor);

            attemptData ??= new PinAttemptData();
            attemptData.FailedCount++;

            if (attemptData.FailedCount >= MAX_ATTEMPTS)
            {
                attemptData.IsLocked = true;
                attemptData.LockoutUntil = DateTime.Now.AddMinutes(LOCKOUT_MINUTES);
            }

            _cache.Set(cacheKey, attemptData, TimeSpan.FromMinutes(LOCKOUT_MINUTES + 1));

            var attemptsLeft = MAX_ATTEMPTS - attemptData.FailedCount;
            if (attemptsLeft > 0)
                return ServiceResult<Staff>.Failure($"Mã PIN không đúng. Còn {attemptsLeft} lần thử. Sai {MAX_ATTEMPTS} lần sẽ khóa {LOCKOUT_MINUTES} phút.");

            return ServiceResult<Staff>.Failure($"Đã bị khóa do nhập sai quá {MAX_ATTEMPTS} lần. Vui lòng thử lại sau {LOCKOUT_MINUTES} phút.");
        }

        private class PinAttemptData
        {
            public int FailedCount { get; set; }
            public bool IsLocked { get; set; }
            public DateTime LockoutUntil { get; set; }
        }
    }
}
