using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Profitability
{
    public sealed class DrinkSizeToppingPolicyService : IDrinkSizeToppingPolicyService
    {
        private readonly AppDbContext _context;

        public DrinkSizeToppingPolicyService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<DrinkSizeToppingPolicyDto>> GetActiveAsync(int drinkSizeId, CancellationToken cancellationToken = default)
        {
            var policies = await _context.DrinkSizeToppingPolicies.AsNoTracking()
                .Where(x => x.DrinkSizeId == drinkSizeId && x.IsActive)
                .Include(x => x.Topping)
                .OrderBy(x => x.Topping.Name)
                .ToListAsync(cancellationToken);
            return policies.Select(Map).ToList();
        }

        public async Task<ServiceResult<DrinkSizeToppingPolicyDto>> UpsertAsync(UpsertDrinkSizeToppingPolicyRequest request, int actorStaffId, CancellationToken cancellationToken = default)
        {
            if (actorStaffId <= 0)
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Không xác định được người thao tác.");
            if (!await IsBusinessOwnerAsync(actorStaffId, cancellationToken))
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Chỉ Chủ doanh nghiệp được cập nhật chính sách topping mặc định.");
            if (request.QuantityPerDrink <= 0)
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Số lượng topping trên mỗi đồ uống phải lớn hơn 0.");
            if (request.IsRequired && !request.IsDefaultSelected)
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Topping bắt buộc phải được chọn mặc định.");
            if (!IsValidCombination(request.PriceTreatment, request.CostTreatment))
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Cặp quy tắc giá bán và giá vốn topping không hợp lệ.");

            var drinkSize = await _context.DrinkSizes.AsNoTracking().FirstOrDefaultAsync(x => x.DrinkSizeId == request.DrinkSizeId && x.Active, cancellationToken);
            var topping = await _context.Toppings.AsNoTracking().FirstOrDefaultAsync(x => x.ToppingId == request.ToppingId && x.Active, cancellationToken);
            if (drinkSize == null || topping == null)
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("DrinkSize hoặc topping không tồn tại/đã ngừng hoạt động.");

            var permitted = await _context.DrinkToppings.AsNoTracking().AnyAsync(x => x.DrinkId == drinkSize.DrinkId && x.ToppingId == request.ToppingId && x.Active, cancellationToken)
                || await _context.DrinkDefaultToppings.AsNoTracking().AnyAsync(x => x.DrinkId == drinkSize.DrinkId && x.ToppingId == request.ToppingId, cancellationToken);
            if (!permitted)
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Topping không thuộc danh mục được phép của đồ uống.");

            DrinkSizeToppingPolicy entity;
            string? oldJson = null;
            var action = "CREATE";
            if (request.PolicyId.HasValue)
            {
                var existing = await _context.DrinkSizeToppingPolicies.Include(x => x.Topping)
                    .FirstOrDefaultAsync(x => x.DrinkSizeToppingPolicyId == request.PolicyId.Value, cancellationToken);
                if (existing == null)
                    return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Không tìm thấy chính sách topping.");
                entity = existing;
                if (string.IsNullOrWhiteSpace(request.ExpectedRowVersion))
                    return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Thiếu RowVersion để cập nhật chính sách.");
                try
                {
                    _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(request.ExpectedRowVersion);
                }
                catch (FormatException)
                {
                    return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("RowVersion không hợp lệ.");
                }
                oldJson = Serialize(entity);
                action = "UPDATE";
                entity.UpdatedByStaffId = actorStaffId;
            }
            else
            {
                entity = new DrinkSizeToppingPolicy { CreatedByStaffId = actorStaffId, CreatedAtUtc = DateTime.UtcNow };
                _context.DrinkSizeToppingPolicies.Add(entity);
            }

            entity.DrinkSizeId = request.DrinkSizeId;
            entity.ToppingId = request.ToppingId;
            entity.IsDefaultSelected = request.IsDefaultSelected;
            entity.IsRequired = request.IsRequired;
            entity.PriceTreatment = request.PriceTreatment;
            entity.CostTreatment = request.CostTreatment;
            entity.QuantityPerDrink = request.QuantityPerDrink;
            entity.IsActive = request.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                _context.DrinkSizeToppingPolicyAudits.Add(new DrinkSizeToppingPolicyAudit
                {
                    DrinkSizeToppingPolicyId = entity.DrinkSizeToppingPolicyId,
                    Action = action,
                    OldDataJson = oldJson,
                    NewDataJson = Serialize(entity),
                    ActorStaffId = actorStaffId,
                    Reason = request.Reason?.Trim(),
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await _context.Entry(entity).Reference(x => x.Topping).LoadAsync(cancellationToken);
                return ServiceResult<DrinkSizeToppingPolicyDto>.Success(Map(entity));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Chính sách đã được người khác cập nhật. Vui lòng tải lại.");
            }
            catch (DbUpdateException ex) when (IsActiveDuplicate(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<DrinkSizeToppingPolicyDto>.Failure("Đã có chính sách active cho topping này trong size.");
            }
        }

        private async Task<bool> IsBusinessOwnerAsync(int staffId, CancellationToken ct) => await _context.Staffs.AsNoTracking()
            .AnyAsync(s => s.StaffId == staffId && s.Active && s.Account.Active
                && s.Account.AccountRoles.Any(ar => ar.Role.Active
                    && (ar.Role.Name == RoleConstants.BusinessOwner
                        || ar.Role.Name == RoleConstants.SystemAdmin)), ct);

        private static bool IsValidCombination(string price, string cost)
        {
            if (!ToppingPriceTreatments.All.Contains(price) || !ToppingCostTreatments.All.Contains(cost)) return false;
            if (cost == ToppingCostTreatments.DisplayOnly) return true;
            return (price == ToppingPriceTreatments.IncludedInBasePrice && cost == ToppingCostTreatments.IncludedInDrinkRecipe)
                || (price == ToppingPriceTreatments.AddToppingPrice && cost == ToppingCostTreatments.AddToppingRecipeCost)
                || (price == ToppingPriceTreatments.IncludedInBasePrice && cost == ToppingCostTreatments.AddToppingRecipeCost);
        }

        private static DrinkSizeToppingPolicyDto Map(DrinkSizeToppingPolicy x) => new()
        {
            PolicyId = x.DrinkSizeToppingPolicyId, DrinkSizeId = x.DrinkSizeId, ToppingId = x.ToppingId,
            ToppingName = x.Topping?.Name ?? string.Empty, ToppingPrice = x.Topping?.Price ?? 0,
            IsDefaultSelected = x.IsDefaultSelected, IsRequired = x.IsRequired,
            PriceTreatment = x.PriceTreatment, CostTreatment = x.CostTreatment,
            QuantityPerDrink = x.QuantityPerDrink, IsActive = x.IsActive,
            RowVersion = x.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(x.RowVersion)
        };

        private static string Serialize(DrinkSizeToppingPolicy x) => JsonSerializer.Serialize(new
        { x.DrinkSizeId, x.ToppingId, x.IsDefaultSelected, x.IsRequired, x.PriceTreatment, x.CostTreatment, x.QuantityPerDrink, x.IsActive });

        private static bool IsActiveDuplicate(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UX_DrinkSizeToppingPolicies_Active", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
