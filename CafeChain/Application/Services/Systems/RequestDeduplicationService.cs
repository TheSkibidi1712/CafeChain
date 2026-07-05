using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Infrastrusture.Interfaces.Systems;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CafeChain.Application.Services.Systems
{
    public class RequestDeduplicationService : IRequestDeduplicationService
    {
        private const string Processing = "PROCESSING";
        private const string Success = "SUCCESS";
        private const string Failed = "FAILED";

        private readonly IRequestDeduplicationRepository _repository;

        public RequestDeduplicationService(IRequestDeduplicationRepository repository)
        {
            _repository = repository;
        }

        public async Task<RequestDeduplicationBeginResult> BeginAsync(
            string? requestKey,
            string actionName,
            int staffId,
            object requestBody,
            int? referenceId = null)
        {
            if (string.IsNullOrWhiteSpace(requestKey))
            {
                throw new InvalidOperationException("RequestKey là bắt buộc.");
            }

            var normalizedKey = requestKey.Trim();
            var existing = await FindExistingAsync(normalizedKey, actionName, staffId);

            if (existing != null)
            {
                return BuildDuplicateResult(existing);
            }

            var now = DateTime.UtcNow;
            var entry = new RequestDeduplication
            {
                RequestKey = normalizedKey,
                ActionName = actionName,
                StaffId = staffId,
                ReferenceId = referenceId,
                Status = Processing,
                RequestBody = Serialize(requestBody),
                CreatedAt = now,
                ExpiredAt = now.AddMinutes(30)
            };

            await _repository.AddAsync(entry);

            try
            {
                await _repository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _repository.Detach(entry);

                existing = await FindExistingAsync(normalizedKey, actionName, staffId);

                if (existing != null)
                {
                    return BuildDuplicateResult(existing);
                }

                throw;
            }

            return new RequestDeduplicationBeginResult
            {
                CanProcess = true,
                Entry = entry
            };
        }

        public async Task MarkSuccessAsync(
            RequestDeduplication entry,
            int referenceId,
            object responseBody)
        {
            entry.Status = Success;
            entry.ReferenceId = referenceId;
            entry.ResponseBody = Serialize(responseBody);

            _repository.Update(entry);

            await _repository.SaveChangesAsync();
        }

        public async Task MarkFailedAsync(
            RequestDeduplication entry,
            object responseBody)
        {
            entry.Status = Failed;
            entry.ResponseBody = Serialize(responseBody);

            _repository.Update(entry);

            await _repository.SaveChangesAsync();
        }

        private async Task<RequestDeduplication?> FindExistingAsync(
            string requestKey,
            string actionName,
            int staffId)
        {
            return await _repository.GetAsync(requestKey, actionName, staffId);
        }

        private static RequestDeduplicationBeginResult BuildDuplicateResult(
            RequestDeduplication existing)
        {
            var message = existing.Status switch
            {
                Processing => "Yêu cầu đang được xử lý, vui lòng không thao tác lại.",
                Success => string.Empty,
                Failed => "Yêu cầu trước đó đã lỗi. Vui lòng tạo RequestKey mới để gửi lại.",
                "EXPIRED" => "RequestKey đã hết hạn. Vui lòng tạo RequestKey mới.",
                _ => "RequestKey đã được sử dụng."
            };

            return new RequestDeduplicationBeginResult
            {
                CanProcess = false,
                IsDuplicate = true,
                Status = existing.Status,
                ReferenceId = existing.ReferenceId,
                ResponseBody = existing.ResponseBody,
                ErrorMessage = message
            };
        }

        private static string Serialize(object value)
        {
            return JsonSerializer.Serialize(
                value,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
        }
    }
}
