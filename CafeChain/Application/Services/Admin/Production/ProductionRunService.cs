using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.Production
{
    /// <summary>
    /// Issue #119 — CreateAndConfirm production intent only (no StoreInventory / ledger mutation).
    /// </summary>
    public sealed class ProductionRunService : IProductionRunService
    {
        public const decimal MinRunCountExclusive = 0m;
        public const decimal MaxRunCount = 9999m;
        public const int MaxNotesLength = 500;
        public const string FingerprintContractVersion = "v1";

        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IInventoryWriterModeService _writerModeService;
        private readonly ILogger<ProductionRunService> _logger;

        public ProductionRunService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            IInventoryWriterModeService writerModeService,
            ILogger<ProductionRunService> logger)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _writerModeService = writerModeService;
            _logger = logger;
        }

        public async Task<ServiceResult<ProductionRunResultDto>> CreateAndConfirmAsync(
            CreateAndConfirmProductionRunRequest request,
            int staffId,
            int staffHomeStoreId)
        {
            if (request == null)
                return Fail(ProductionRunFailureCodes.InvalidRequest, "Yêu cầu không hợp lệ.");

            if (staffId <= 0)
                return Fail(ProductionRunFailureCodes.StaffUnauthorized, "Thiếu thông tin nhân viên.");

            if (staffHomeStoreId <= 0)
                return Fail(ProductionRunFailureCodes.StoreUnauthorized, "Thiếu cửa hàng của nhân viên.");

            if (!request.RequestKey.HasValue || request.RequestKey.Value == Guid.Empty)
            {
                return Fail(
                    ProductionRunFailureCodes.InvalidRequestKey,
                    "RequestKey là bắt buộc (GUID).");
            }

            var requestKey = request.RequestKey.Value;

            if (request.RequestedRunCount <= MinRunCountExclusive || request.RequestedRunCount > MaxRunCount)
            {
                return Fail(
                    ProductionRunFailureCodes.InvalidRunCount,
                    "Số mẻ nấu phải > 0 và ≤ 9999.");
            }

            if (request.RecipeId <= 0)
                return Fail(ProductionRunFailureCodes.RecipeNotFound, "RecipeId không hợp lệ.");

            var notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim();
            if (notes != null && notes.Length > MaxNotesLength)
                return Fail(ProductionRunFailureCodes.InvalidRequest, $"Ghi chú tối đa {MaxNotesLength} ký tự.");

            var storeId = request.StoreId is > 0 ? request.StoreId.Value : staffHomeStoreId;

            var storeAuthorized = await AuthorizeStoreAsync(staffId, staffHomeStoreId, storeId);
            if (!storeAuthorized.IsSuccess)
                return Fail(storeAuthorized.ErrorCode!, storeAuthorized.Message);

            var fingerprint = BuildFingerprint(storeId, request.RecipeId, request.RequestedRunCount);

            // Fast path: existing row (lost-response / retry)
            var existingBefore = await FindByKeyAsync(storeId, requestKey);
            if (existingBefore != null)
                return await ReplayOrReuseAsync(existingBefore, fingerprint);

            // Writer mode (#118) — intent only; no stock mutation
            var modeStatus = await _writerModeService.GetStatusAsync(storeId);
            if (!modeStatus.IsSuccess || modeStatus.Data == null)
            {
                return Fail(
                    ProductionRunFailureCodes.MissingWriterConfiguration,
                    modeStatus.Message ?? "Cửa hàng chưa có cấu hình chế độ ghi kho BTP.");
            }

            switch (modeStatus.Data.WriterMode)
            {
                case InventoryWriterMode.LegacyRecipe:
                    break;
                case InventoryWriterMode.Blocked:
                    return Fail(
                        ProductionRunFailureCodes.ModeBlocked,
                        "Kho BTP của cửa hàng đang bị khóa; không thể ghi nhận lệnh sơ chế.");
                case InventoryWriterMode.PreparedItem:
                    return Fail(
                        ProductionRunFailureCodes.ProductionWriterNotReady,
                        "Production PreparedItem writer chưa sẵn sàng (114C).");
                default:
                    return Fail(
                        ProductionRunFailureCodes.ProductionWriterNotReady,
                        "Chế độ ghi kho không hỗ trợ ghi nhận lệnh sơ chế.");
            }

            var staffOk = await _context.Staffs.AsNoTracking()
                .AnyAsync(s => s.StaffId == staffId && s.Active);
            if (!staffOk)
                return Fail(ProductionRunFailureCodes.StaffUnauthorized, "Nhân viên không hoạt động.");

            // New create only: same eligibility as production Create dropdown (Active == true).
            // Replay of an existing RequestKey does not re-check Active (archived-after-confirm OK).
            var recipeEligible = await _context.Recipes.AsNoTracking()
                .AnyAsync(r => r.RecipeId == request.RecipeId && r.Active);
            if (!recipeEligible)
            {
                return Fail(
                    ProductionRunFailureCodes.RecipeNotFound,
                    "Công thức không hợp lệ hoặc không còn hoạt động để tạo lệnh sơ chế.");
            }

            var now = DateTime.UtcNow;
            var run = new ProductionRun
            {
                StoreId = storeId,
                RecipeId = request.RecipeId,
                RequestedRunCount = request.RequestedRunCount,
                RequestKey = requestKey,
                RequestFingerprint = fingerprint,
                Status = ProductionRunStatus.Confirmed,
                Notes = notes,
                CreatedByStaffId = staffId,
                CreatedAt = now,
                ConfirmedAt = now
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Re-check under transaction for concurrent winner
                var existingInTx = await _context.ProductionRuns
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == requestKey);

                if (existingInTx != null)
                {
                    await transaction.RollbackAsync();
                    return await ReplayOrReuseAsync(existingInTx, fingerprint);
                }

                _context.ProductionRuns.Add(run);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var created = await ToResultAsync(run, wasReplay: false);
                LogTransition(run, wasReplay: false);
                return ServiceResult<ProductionRunResultDto>.Success(
                    created,
                    "Lệnh sơ chế đã được ghi nhận. Tồn kho chưa được cập nhật.");
            }
            catch (DbUpdateException ex)
            {
                // Unique conflict / poisoned tx — rollback then re-query
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore if already rolled back
                }

                DetachAllTracked();

                var raced = await FindByKeyAsync(storeId, requestKey);
                if (raced != null)
                {
                    _logger.LogInformation(
                        ex,
                        "[ProductionRun] Unique conflict resolved StoreId={StoreId} Key={RequestKey}",
                        storeId,
                        requestKey);
                    return await ReplayOrReuseAsync(raced, fingerprint);
                }

                _logger.LogError(
                    ex,
                    "[ProductionRun] CreateAndConfirm failed StoreId={StoreId} RecipeId={RecipeId} Key={RequestKey}",
                    storeId,
                    request.RecipeId,
                    requestKey);
                return Fail(ProductionRunFailureCodes.InvalidRequest, "Không thể ghi nhận lệnh sơ chế. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                DetachAllTracked();

                _logger.LogError(
                    ex,
                    "[ProductionRun] CreateAndConfirm failed StoreId={StoreId} RecipeId={RecipeId} Key={RequestKey}",
                    storeId,
                    request.RecipeId,
                    requestKey);
                return Fail(ProductionRunFailureCodes.InvalidRequest, "Không thể ghi nhận lệnh sơ chế. Vui lòng thử lại.");
            }
        }

        public async Task<IReadOnlyList<ProductionRunHistoryItemDto>> GetRecentAsync(int storeId, int take = 5)
        {
            if (storeId <= 0 || take <= 0)
                return Array.Empty<ProductionRunHistoryItemDto>();

            take = Math.Min(take, 20);

            return await (
                from run in _context.ProductionRuns.AsNoTracking()
                where run.StoreId == storeId
                join recipe in _context.Recipes.AsNoTracking() on run.RecipeId equals recipe.RecipeId into recipes
                from recipe in recipes.DefaultIfEmpty()
                join staff in _context.Staffs.AsNoTracking() on run.CreatedByStaffId equals staff.StaffId into staffs
                from staff in staffs.DefaultIfEmpty()
                join store in _context.Stores.AsNoTracking() on run.StoreId equals store.StoreId into stores
                from store in stores.DefaultIfEmpty()
                orderby run.CreatedAt descending
                select new ProductionRunHistoryItemDto
                {
                    ProductionRunId = run.ProductionRunId,
                    StoreId = run.StoreId,
                    StoreName = store != null ? store.Name : null,
                    RecipeId = run.RecipeId,
                    RecipeName = recipe != null ? recipe.Name : null,
                    RequestedRunCount = run.RequestedRunCount,
                    Status = "CONFIRMED",
                    ConfirmedAt = run.ConfirmedAt,
                    CreatedByStaffId = run.CreatedByStaffId,
                    ActorName = staff != null ? staff.FullName : null,
                    StockApplied = false
                })
                .Take(take)
                .ToListAsync();
        }

        private async Task<ServiceResult> AuthorizeStoreAsync(int staffId, int staffHomeStoreId, int storeId)
        {
            var storeActive = await _context.Stores.AsNoTracking()
                .AnyAsync(s => s.StoreId == storeId && s.Active);
            if (!storeActive)
                return ServiceResult.Failure("Không tìm thấy cửa hàng.", errorCode: ProductionRunFailureCodes.StoreNotFound);

            if (storeId == staffHomeStoreId)
                return ServiceResult.Success();

            // Multi-store: only if scope authorization allows
            if (await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId))
                return ServiceResult.Success();

            return ServiceResult.Failure(
                "Bạn không có quyền ghi nhận lệnh sơ chế cho cửa hàng này.",
                errorCode: ProductionRunFailureCodes.StoreUnauthorized);
        }

        private Task<ProductionRun?> FindByKeyAsync(int storeId, Guid requestKey)
            => _context.ProductionRuns.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.RequestKey == requestKey);

        private async Task<ServiceResult<ProductionRunResultDto>> ReplayOrReuseAsync(
            ProductionRun existing,
            string fingerprint)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Fail(
                    ProductionRunFailureCodes.IdempotencyKeyReused,
                    "RequestKey đã được dùng với nội dung khác. Tạo RequestKey mới cho thao tác mới.");
            }

            var dto = await ToResultAsync(existing, wasReplay: true);
            LogTransition(existing, wasReplay: true);
            return ServiceResult<ProductionRunResultDto>.Success(
                dto,
                "Lệnh sơ chế đã được ghi nhận trước đó.");
        }

        private async Task<ProductionRunResultDto> ToResultAsync(ProductionRun run, bool wasReplay)
        {
            var recipeName = await _context.Recipes.AsNoTracking()
                .Where(r => r.RecipeId == run.RecipeId)
                .Select(r => r.Name)
                .FirstOrDefaultAsync();

            return new ProductionRunResultDto
            {
                ProductionRunId = run.ProductionRunId,
                StoreId = run.StoreId,
                RecipeId = run.RecipeId,
                RequestedRunCount = run.RequestedRunCount,
                Status = "CONFIRMED",
                ConfirmedAt = run.ConfirmedAt,
                WasReplay = wasReplay,
                StockApplied = false,
                MessageKey = wasReplay ? "ProductionRun.Replay" : "ProductionRun.Confirmed",
                RecipeName = recipeName
            };
        }

        private void LogTransition(ProductionRun run, bool wasReplay)
        {
            _logger.LogInformation(
                "[ProductionRun] ProductionRunId={ProductionRunId} StoreId={StoreId} RecipeId={RecipeId} Status={Status} WasReplay={WasReplay} RequestKey={RequestKey}",
                run.ProductionRunId,
                run.StoreId,
                run.RecipeId,
                run.Status,
                wasReplay,
                run.RequestKey);
        }

        private void DetachAllTracked()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }

        /// <summary>
        /// SHA-256 over v1|StoreId|RecipeId|RequestedRunCount(G29 invariant).
        /// 1, 1.0, 1.00000 produce the same fingerprint.
        /// </summary>
        public static string BuildFingerprint(int storeId, int recipeId, decimal requestedRunCount)
        {
            var countText = requestedRunCount.ToString("G29", CultureInfo.InvariantCulture);
            var payload = $"{FingerprintContractVersion}|{storeId}|{recipeId}|{countText}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static ServiceResult<ProductionRunResultDto> Fail(string code, string message)
            => ServiceResult<ProductionRunResultDto>.Failure(message, errorCode: code);
    }
}
