using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Infrastrusture.Interfaces.Systems;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

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
            var serializedBody = SerializeCanonical(requestBody);
            var payloadHash = ComputeSha256(serializedBody);
            var existing = await FindExistingAsync(normalizedKey, actionName, staffId);

            if (existing != null)
            {
                return BuildDuplicateResult(existing, payloadHash);
            }

            var now = DateTime.UtcNow;
            var entry = new RequestDeduplication
            {
                RequestKey = normalizedKey,
                ActionName = actionName,
                StaffId = staffId,
                ReferenceId = referenceId,
                Status = Processing,
                RequestBody = serializedBody,
                PayloadHash = payloadHash,
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
                    return BuildDuplicateResult(existing, payloadHash);
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
            // Failure belongs to the rolled-back business transaction. Do not
            // persist FAILED: the same key must be available for a safe retry.
            _repository.Detach(entry);
            await Task.CompletedTask;
        }

        private async Task<RequestDeduplication?> FindExistingAsync(
            string requestKey,
            string actionName,
            int staffId)
        {
            return await _repository.GetAsync(requestKey, actionName, staffId);
        }

        private static RequestDeduplicationBeginResult BuildDuplicateResult(
            RequestDeduplication existing,
            string payloadHash)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
            {
                return new RequestDeduplicationBeginResult
                {
                    CanProcess = false,
                    IsDuplicate = true,
                    Status = existing.Status,
                    ReferenceId = existing.ReferenceId,
                    ErrorCode = "IDEMPOTENCY_KEY_REUSED",
                    ErrorMessage = "RequestKey đã được dùng với payload khác."
                };
            }

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
                ErrorCode = existing.Status switch
                {
                    Processing => "REQUEST_IN_PROGRESS",
                    Failed => "REQUEST_PREVIOUSLY_FAILED",
                    "EXPIRED" => "REQUEST_EXPIRED",
                    Success => null,
                    _ => "REQUEST_KEY_UNAVAILABLE"
                },
                ErrorMessage = message
            };
        }

        private static string SerializeCanonical(object value)
        {
            using var document = JsonDocument.Parse(Serialize(value));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteCanonical(writer, document.RootElement);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonical(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                        WriteCanonical(writer, item);
                    writer.WriteEndArray();
                    break;
                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private static string ComputeSha256(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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
