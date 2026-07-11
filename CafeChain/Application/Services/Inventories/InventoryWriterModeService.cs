using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class InventoryWriterModeService : IInventoryWriterModeService
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physicalUnitConversion;
        private readonly IReadOnlyDictionary<string, IInventoryWriterCapabilityProvider> _capabilities;

        public InventoryWriterModeService(
            AppDbContext context,
            IPhysicalUnitConversionService physicalUnitConversion,
            IEnumerable<IInventoryWriterCapabilityProvider> capabilityProviders)
        {
            _context = context;
            _physicalUnitConversion = physicalUnitConversion;
            _capabilities = capabilityProviders
                .GroupBy(x => x.GetStatus().CapabilityId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        }

        public async Task<ServiceResult<InventoryWriterModeSnapshot>> AcquireSnapshotAsync(int storeId)
        {
            var transaction = _context.Database.CurrentTransaction;
            if (transaction == null)
            {
                return ServiceResult<InventoryWriterModeSnapshot>.Failure(
                    "Thao tác BTP phải chạy trong một giao dịch kho.",
                    errorCode: InventoryWriterFailureCodes.MissingTransaction);
            }

            var configuration = await LoadConfigurationForUpdateAsync(storeId);
            if (configuration == null)
            {
                return ServiceResult<InventoryWriterModeSnapshot>.Failure(
                    "Cửa hàng chưa có cấu hình chế độ ghi kho BTP. Hệ thống đã khóa thao tác để bảo vệ tồn kho.",
                    errorCode: InventoryWriterFailureCodes.MissingConfiguration);
            }

            return ServiceResult<InventoryWriterModeSnapshot>.Success(new InventoryWriterModeSnapshot(
                configuration.StoreId,
                configuration.WriterMode,
                configuration.RowVersion.ToArray(),
                transaction.TransactionId));
        }

        public bool IsSnapshotValidForCurrentTransaction(InventoryWriterModeSnapshot snapshot, int storeId)
        {
            var transaction = _context.Database.CurrentTransaction;
            return transaction != null
                && snapshot.StoreId == storeId
                && snapshot.TransactionId == transaction.TransactionId;
        }

        public ServiceResult EnsureLegacyBtpWriteAllowed(InventoryWriterModeSnapshot snapshot, int storeId)
        {
            if (!IsSnapshotValidForCurrentTransaction(snapshot, storeId))
            {
                return ServiceResult.Failure(
                    "Mode snapshot không thuộc giao dịch kho hiện tại.",
                    errorCode: InventoryWriterFailureCodes.InvalidSnapshot);
            }

            return snapshot.WriterMode switch
            {
                InventoryWriterMode.LegacyRecipe => ServiceResult.Success(),
                InventoryWriterMode.Blocked => ServiceResult.Failure(
                    "Kho BTP của cửa hàng đang bị khóa để đối soát.",
                    errorCode: InventoryWriterFailureCodes.ModeBlocked),
                _ => ServiceResult.Failure(
                    "Writer RecipeId cũ không được phép ghi khi cửa hàng dùng PreparedItem.",
                    errorCode: InventoryWriterFailureCodes.LegacyWriterForbidden)
            };
        }

        public async Task<ServiceResult<InventoryWriterModeStatusDto>> GetStatusAsync(int storeId)
        {
            var configuration = await _context.StoreInventoryWriterConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StoreId == storeId);

            return configuration == null
                ? ServiceResult<InventoryWriterModeStatusDto>.Failure(
                    "Không tìm thấy cấu hình writer kho cho cửa hàng.",
                    errorCode: InventoryWriterFailureCodes.MissingConfiguration)
                : ServiceResult<InventoryWriterModeStatusDto>.Success(ToStatus(configuration));
        }

        public async Task<InventoryWriterReadinessReport> EvaluateReadinessAsync(int storeId)
        {
            var blockers = new List<InventoryReadinessBlocker>();

            if (!await _context.StoreInventoryWriterConfigurations.AsNoTracking().AnyAsync(x => x.StoreId == storeId))
            {
                blockers.Add(new InventoryReadinessBlocker(
                    InventoryWriterFailureCodes.MissingConfiguration,
                    "Cửa hàng chưa có cấu hình writer kho."));
            }

            foreach (var capabilityId in InventoryWriterCapabilityIds.Required)
            {
                if (!_capabilities.TryGetValue(capabilityId, out var provider))
                {
                    blockers.Add(new InventoryReadinessBlocker(
                        $"CAPABILITY_MISSING:{capabilityId}",
                        $"Capability {capabilityId} chưa được triển khai."));
                    continue;
                }

                var status = provider.GetStatus();
                if (!status.Ready)
                {
                    blockers.Add(new InventoryReadinessBlocker(
                        status.BlockerCode ?? $"CAPABILITY_NOT_READY:{capabilityId}",
                        status.BlockerMessage ?? $"Capability {capabilityId} chưa sẵn sàng."));
                }
            }

            var rows = await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Recipe).ThenInclude(x => x.OutputUnit)
                .Include(x => x.PreparedItem).ThenInclude(x => x!.BaseUnit)
                .Where(x => x.StoreId == storeId && x.IngredientId == null)
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync();

            foreach (var row in rows)
            {
                if (row.RecipeId.HasValue)
                {
                    if (row.Recipe?.PreparedItemId == null)
                    {
                        blockers.Add(Block("MISSING_EXPLICIT_MAPPING", row, "Recipe BTP chưa có PreparedItem mapping rõ ràng."));
                    }
                    else if (row.Recipe.OutputQuantity is null or <= 0 || !row.Recipe.OutputUnitId.HasValue)
                    {
                        blockers.Add(Block("INVALID_RECIPE_OUTPUT_CONTRACT", row, "Recipe BTP thiếu output contract hợp lệ."));
                    }
                    else
                    {
                        var preparedItem = row.PreparedItemId == row.Recipe.PreparedItemId
                            ? row.PreparedItem
                            : await _context.PreparedItems.AsNoTracking()
                                .Include(x => x.BaseUnit)
                                .FirstOrDefaultAsync(x => x.PreparedItemId == row.Recipe.PreparedItemId.Value);

                        if (preparedItem == null || !preparedItem.Active || preparedItem.BaseUnit == null || !preparedItem.BaseUnit.Active)
                        {
                            blockers.Add(Block("INVALID_PREPARED_ITEM", row, "PreparedItem mapping không hoạt động hoặc thiếu base unit."));
                        }
                        else
                        {
                            var conversion = await _physicalUnitConversion.ConvertAsync(
                                1m,
                                row.Recipe.OutputUnitId.Value,
                                preparedItem.BaseUnitId);
                            if (!conversion.IsSuccess)
                                blockers.Add(Block("UNIT_MISMATCH", row, "Output unit không quy đổi được sang PreparedItem base unit."));
                        }
                    }
                }

                if (row.BtpIdentityState != BtpIdentityState.Superseded
                    && row.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                    blockers.Add(Block("QUANTITY_SEMANTICS_NOT_CONFIRMED", row, "Chưa xác nhận quantity theo PreparedItem base unit."));

                if (row.BtpIdentityState == BtpIdentityState.Canonical
                    && (!row.PreparedItemId.HasValue
                        || row.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed))
                {
                    blockers.Add(Block("INVALID_CANONICAL_ROW", row, "Dòng canonical không hợp lệ."));
                }
            }

            var activeIdentityRows = rows
                .Where(x => x.BtpIdentityState != BtpIdentityState.Superseded)
                .Select(x => new
                {
                    Row = x,
                    EffectivePreparedItemId = x.PreparedItemId ?? x.Recipe?.PreparedItemId
                })
                .Where(x => x.EffectivePreparedItemId.HasValue)
                .ToList();

            foreach (var group in activeIdentityRows.GroupBy(x => x.EffectivePreparedItemId!.Value))
            {
                if (group.Count() > 1)
                    blockers.Add(new InventoryReadinessBlocker(
                        $"COLLISION:PREPARED_ITEM:{group.Key}",
                        $"PreparedItem #{group.Key} có nhiều dòng tồn chưa supersede."));
                if (!group.Any(x => x.Row.BtpIdentityState == BtpIdentityState.Canonical))
                    blockers.Add(new InventoryReadinessBlocker(
                        $"MISSING_CANONICAL_ROW:PREPARED_ITEM:{group.Key}",
                        $"PreparedItem #{group.Key} chưa có dòng canonical."));
                if (HasConflict(group.Select(x => x.Row.MinStockLevel)))
                    blockers.Add(new InventoryReadinessBlocker(
                        $"MIN_STOCK_CONFLICT:PREPARED_ITEM:{group.Key}",
                        $"PreparedItem #{group.Key} có MinStockLevel xung đột."));
                if (HasConflict(group.Select(x => x.Row.MaxNegativeQty)))
                    blockers.Add(new InventoryReadinessBlocker(
                        $"MAX_NEGATIVE_CONFLICT:PREPARED_ITEM:{group.Key}",
                        $"PreparedItem #{group.Key} có MaxNegativeQty xung đột."));
            }

            var ordered = blockers
                .GroupBy(x => new { x.Code, x.Message })
                .Select(x => x.First())
                .OrderBy(x => x.Code, StringComparer.Ordinal)
                .ThenBy(x => x.Message, StringComparer.Ordinal)
                .ToList();

            return new InventoryWriterReadinessReport
            {
                StoreId = storeId,
                Ready = ordered.Count == 0,
                Blockers = ordered,
                ReadinessHash = ComputeHash(storeId, ordered)
            };
        }

        public async Task<InventoryWriterModeTransitionResult> TransitionAsync(
            InventoryWriterModeTransitionRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var configuration = await LoadConfigurationForUpdateAsync(request.StoreId);
            if (configuration == null)
            {
                await transaction.RollbackAsync();
                return Failed("Không tìm thấy cấu hình writer kho.", InventoryWriterFailureCodes.MissingConfiguration);
            }

            var readiness = await EvaluateReadinessAsync(request.StoreId);
            var failureCode = await ValidateTransitionAsync(configuration, request, readiness);
            if (failureCode != null)
            {
                if (failureCode == InventoryWriterFailureCodes.Unauthorized
                    && !await _context.Accounts.AsNoTracking().AnyAsync(x => x.AccountId == request.ActorAccountId))
                {
                    await transaction.RollbackAsync();
                    return Failed(TransitionFailureMessage(failureCode), failureCode, readiness, ToStatus(configuration));
                }

                AddAudit(configuration, request, readiness, false, failureCode);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Failed(TransitionFailureMessage(failureCode), failureCode, readiness, ToStatus(configuration));
            }

            var fromMode = configuration.WriterMode;
            configuration.WriterMode = request.TargetMode;
            configuration.UpdatedAt = DateTime.UtcNow;
            if (request.TargetMode == InventoryWriterMode.PreparedItem)
                configuration.HasEverActivatedPreparedItem = true;

            AddAudit(configuration, request, readiness, true, null, fromMode);

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new InventoryWriterModeTransitionResult
                {
                    Succeeded = true,
                    Message = "Đã cập nhật chế độ writer kho.",
                    Status = ToStatus(configuration),
                    Readiness = readiness
                };
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Failed(
                    "Cấu hình writer vừa được thay đổi bởi giao dịch khác.",
                    InventoryWriterFailureCodes.StaleConfiguration,
                    readiness);
            }
        }

        private async Task<string?> ValidateTransitionAsync(
            StoreInventoryWriterConfiguration configuration,
            InventoryWriterModeTransitionRequest request,
            InventoryWriterReadinessReport readiness)
        {
            if (!await CanTransitionAsync(request.ActorAccountId))
                return InventoryWriterFailureCodes.Unauthorized;
            if (string.IsNullOrWhiteSpace(request.Reason))
                return InventoryWriterFailureCodes.InvalidTransition;
            if (configuration.WriterMode != request.ExpectedCurrentMode
                || request.ExpectedRowVersion.Length == 0
                || !configuration.RowVersion.SequenceEqual(request.ExpectedRowVersion))
                return InventoryWriterFailureCodes.StaleConfiguration;
            if (configuration.WriterMode == request.TargetMode)
                return InventoryWriterFailureCodes.InvalidTransition;

            var allowed = (configuration.WriterMode, request.TargetMode) switch
            {
                (InventoryWriterMode.LegacyRecipe, InventoryWriterMode.Blocked) => true,
                (InventoryWriterMode.Blocked, InventoryWriterMode.LegacyRecipe) => !configuration.HasEverActivatedPreparedItem,
                (InventoryWriterMode.LegacyRecipe, InventoryWriterMode.PreparedItem) => readiness.Ready,
                (InventoryWriterMode.PreparedItem, InventoryWriterMode.Blocked) => true,
                (InventoryWriterMode.Blocked, InventoryWriterMode.PreparedItem) => readiness.Ready,
                _ => false
            };

            if (!allowed)
            {
                if (request.TargetMode == InventoryWriterMode.PreparedItem && !readiness.Ready)
                    return InventoryWriterFailureCodes.ReadinessFailed;
                return InventoryWriterFailureCodes.InvalidTransition;
            }

            if (request.TargetMode == InventoryWriterMode.PreparedItem
                && !string.Equals(request.ReadinessHash, readiness.ReadinessHash, StringComparison.Ordinal))
                return InventoryWriterFailureCodes.ReadinessFailed;

            return null;
        }

        private async Task<bool> CanTransitionAsync(int accountId)
        {
            return accountId > 0 && await _context.Accounts
                .AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Active)
                .SelectMany(x => x.AccountRoles)
                .AnyAsync(x => x.Role != null && x.Role.Active
                    && (x.Role.Name == RoleConstants.SystemAdmin || x.Role.Name == RoleConstants.BusinessOwner));
        }

        private async Task<StoreInventoryWriterConfiguration?> LoadConfigurationForUpdateAsync(int storeId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventoryWriterConfigurations
                    .FromSqlInterpolated($"SELECT * FROM StoreInventoryWriterConfigurations WITH (UPDLOCK, HOLDLOCK) WHERE StoreId = {storeId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.StoreInventoryWriterConfigurations
                .SingleOrDefaultAsync(x => x.StoreId == storeId);
        }

        private void AddAudit(
            StoreInventoryWriterConfiguration configuration,
            InventoryWriterModeTransitionRequest request,
            InventoryWriterReadinessReport readiness,
            bool succeeded,
            string? failureCode,
            InventoryWriterMode? fromMode = null)
        {
            _context.InventoryWriterModeTransitions.Add(new InventoryWriterModeTransition
            {
                StoreId = configuration.StoreId,
                FromMode = fromMode ?? configuration.WriterMode,
                ToMode = request.TargetMode,
                ActorAccountId = request.ActorAccountId,
                Reason = request.Reason.Trim(),
                ReadinessHash = readiness.ReadinessHash,
                ReadinessSnapshotJson = JsonSerializer.Serialize(readiness.Blockers),
                RequestedAt = DateTime.UtcNow,
                AppliedAt = succeeded ? DateTime.UtcNow : null,
                Succeeded = succeeded,
                FailureCode = failureCode
            });
        }

        private static InventoryReadinessBlocker Block(string code, Models.Stores.StoreInventory row, string message) =>
            new($"{code}:STORE_INVENTORY:{row.StoreInventoryId}", message);

        private static bool HasConflict(IEnumerable<decimal?> values) => values.Distinct().Skip(1).Any();

        private static string ComputeHash(int storeId, IReadOnlyList<InventoryReadinessBlocker> blockers)
        {
            var payload = JsonSerializer.Serialize(new
            {
                StoreId = storeId,
                Blockers = blockers.Select(x => new { x.Code, x.Message }).ToArray()
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        private static InventoryWriterModeStatusDto ToStatus(StoreInventoryWriterConfiguration x) => new()
        {
            StoreId = x.StoreId,
            WriterMode = x.WriterMode,
            HasEverActivatedPreparedItem = x.HasEverActivatedPreparedItem,
            RowVersion = x.RowVersion.ToArray(),
            UpdatedAt = x.UpdatedAt
        };

        private static InventoryWriterModeTransitionResult Failed(
            string message,
            string failureCode,
            InventoryWriterReadinessReport? readiness = null,
            InventoryWriterModeStatusDto? status = null) => new()
            {
                Succeeded = false,
                Message = message,
                FailureCode = failureCode,
                Readiness = readiness,
                Status = status
            };

        private static string TransitionFailureMessage(string code) => code switch
        {
            InventoryWriterFailureCodes.Unauthorized => "Bạn không có quyền đổi chế độ writer kho.",
            InventoryWriterFailureCodes.StaleConfiguration => "Cấu hình writer đã thay đổi. Vui lòng tải lại.",
            InventoryWriterFailureCodes.ReadinessFailed => "PreparedItem writer chưa đạt điều kiện sẵn sàng.",
            _ => "Chuyển chế độ writer không hợp lệ."
        };
    }
}
