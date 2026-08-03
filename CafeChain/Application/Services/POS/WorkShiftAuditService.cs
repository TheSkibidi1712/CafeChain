using System.Text.Json;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Data;
using CafeChain.Models.Inventories.Auditing;

namespace CafeChain.Application.Services.POS;

public sealed class WorkShiftAuditService : IWorkShiftAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public WorkShiftAuditService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task WriteAsync(
        string action,
        int workShiftId,
        int actorStaffId,
        object? oldData,
        object? newData,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "WorkShifts",
            RecordId = workShiftId,
            Action = action,
            OldData = oldData == null ? null : JsonSerializer.Serialize(oldData, JsonOptions),
            NewData = newData == null ? null : JsonSerializer.Serialize(newData, JsonOptions),
            UserId = actorStaffId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteOtpAsync(
        string action,
        int otpChallengeId,
        int actorStaffId,
        object? data,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "OtpChallenges",
            RecordId = otpChallengeId,
            Action = action,
            OldData = null,
            NewData = data == null ? null : JsonSerializer.Serialize(data, JsonOptions),
            UserId = actorStaffId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
