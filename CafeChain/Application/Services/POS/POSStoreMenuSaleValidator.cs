using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.POS
{
    public sealed class POSStoreMenuSaleValidator : IPOSStoreMenuSaleValidator
    {
        private readonly AppDbContext _context;
        private readonly IPOSCatalogSnapshotService _catalog;
        private POSCatalogSnapshotDto? _cachedSnapshot;
        private int _cachedStoreId;
        private DateTime _cachedAsOfUtc;

        public POSStoreMenuSaleValidator(AppDbContext context, IPOSCatalogSnapshotService catalog)
        {
            _context = context;
            _catalog = catalog;
        }

        public async Task<ServiceResult<POSAcceptedSaleLineDto>> ValidateOnlineAsync(
            POSOrderItemDto item,
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            var requiredError = ValidateRequiredSnapshot(item);
            if (requiredError != null)
                return requiredError;

            var snapshot = _cachedSnapshot != null
                && _cachedStoreId == storeId
                && _cachedAsOfUtc == asOfUtc
                    ? _cachedSnapshot
                    : await _catalog.BuildAsync(storeId, asOfUtc, cancellationToken);
            _cachedSnapshot = snapshot;
            _cachedStoreId = storeId;
            _cachedAsOfUtc = asOfUtc;
            var menuItem = snapshot.MenuItems.SingleOrDefault(x => x.Id == item.DrinkId);
            var size = menuItem?.Sizes.SingleOrDefault(x =>
                x.StoreMenuItemId == item.StoreMenuItemId
                && x.DrinkSizeId == item.DrinkSizeId
                && x.SizeId == item.SizeId);

            if (menuItem == null || size == null)
            {
                return Fail(
                    "Món hoặc size không còn thuộc menu của cửa hàng. Vui lòng làm mới giỏ hàng.",
                    POSCatalogSaleErrorCodes.SnapshotInvalid);
            }

            if (snapshot.Version != item.CatalogVersion
                || size.Price != item.AcceptedBasePrice
                || size.RecipeId != item.RecipeIdSnapshot
                || !string.Equals(size.PriceSource, item.PriceSource, StringComparison.Ordinal))
            {
                return Fail(
                    "Menu hoặc giá bán đã thay đổi. Vui lòng làm mới và xác nhận lại giỏ hàng.",
                    POSCatalogSaleErrorCodes.SnapshotStale);
            }

            if (!size.IsAvailable)
            {
                return Fail(
                    size.AvailabilityReason ?? "Món hiện không khả dụng tại cửa hàng.",
                    POSCatalogSaleErrorCodes.ItemUnavailable);
            }

            var toppingResult = ValidateOnlineToppings(item, menuItem, size);
            if (!toppingResult.IsSuccess)
                return ServiceResult<POSAcceptedSaleLineDto>.Failure(
                    toppingResult.Message,
                    errorCode: toppingResult.ErrorCode);

            var acceptedToppings = toppingResult.Data!;
            var expectedUnitPrice = Money(size.Price + acceptedToppings.Sum(x => x.AcceptedPrice));
            if (item.AcceptedUnitPrice != expectedUnitPrice)
            {
                return Fail(
                    "Tổng tiền món không khớp catalog. Vui lòng làm mới giỏ hàng.",
                    POSCatalogSaleErrorCodes.SnapshotStale);
            }

            return ServiceResult<POSAcceptedSaleLineDto>.Success(new POSAcceptedSaleLineDto
            {
                StoreMenuItemId = size.StoreMenuItemId,
                DrinkSizeId = size.DrinkSizeId,
                DrinkId = menuItem.Id,
                SizeId = size.SizeId,
                RecipeId = size.RecipeId,
                DrinkName = menuItem.Name,
                SizeName = size.SizeName,
                AcceptedBasePrice = size.Price,
                AcceptedUnitPrice = expectedUnitPrice,
                PriceSource = size.PriceSource,
                CatalogVersion = snapshot.Version,
                Toppings = acceptedToppings
            });
        }

        public async Task<ServiceResult<POSAcceptedSaleLineDto>> ValidateOfflineAsync(
            POSOrderItemDto item,
            int storeId,
            CancellationToken cancellationToken = default)
        {
            var requiredError = ValidateRequiredSnapshot(item);
            if (requiredError != null)
                return requiredError;

            if (item.CatalogVersion <= 0
                || item.AcceptedBasePrice < 0
                || item.AcceptedUnitPrice < 0
                || (item.PriceSource != StoreMenuPriceSources.Global
                    && item.PriceSource != StoreMenuPriceSources.StoreOverride))
            {
                return Fail("Snapshot giá offline không hợp lệ.", POSCatalogSaleErrorCodes.SnapshotInvalid);
            }

            var menuRow = await _context.StoreMenuItems.AsNoTracking()
                .Include(x => x.DrinkSize).ThenInclude(x => x.Drink)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Size)
                .SingleOrDefaultAsync(x =>
                    x.StoreMenuItemId == item.StoreMenuItemId
                    && x.StoreId == storeId
                    && x.DrinkSizeId == item.DrinkSizeId,
                    cancellationToken);

            if (menuRow == null
                || menuRow.DrinkSize.DrinkId != item.DrinkId
                || menuRow.DrinkSize.SizeId != item.SizeId)
            {
                return Fail(
                    "Snapshot offline không khớp món/size/menu cửa hàng gốc.",
                    POSCatalogSaleErrorCodes.SnapshotInvalid);
            }

            var drinkRecipeMatches = await _context.Recipes.AsNoTracking().AnyAsync(x =>
                x.RecipeId == item.RecipeIdSnapshot
                && x.DrinkId == item.DrinkId
                && x.ToppingId == null
                && (x.SizeId == item.SizeId || x.SizeId == null), cancellationToken);
            if (!drinkRecipeMatches)
            {
                return Fail(
                    "Snapshot công thức đồ uống không khớp món và size gốc.",
                    POSCatalogSaleErrorCodes.SnapshotInvalid);
            }

            var selected = item.Toppings ?? new List<POSOrderToppingDto>();
            if (selected.GroupBy(x => x.ToppingId).Any(x => x.Count() > 1)
                || selected.Any(x => !IsValidOfflineToppingSnapshot(x)))
            {
                return Fail("Snapshot topping offline không hợp lệ.", POSCatalogSaleErrorCodes.ToppingInvalid);
            }

            var toppingIds = selected.Select(x => x.ToppingId).ToArray();
            var toppingRows = await (
                from drinkTopping in _context.DrinkToppings.AsNoTracking()
                join storeTopping in _context.StoreToppings.AsNoTracking()
                    on drinkTopping.ToppingId equals storeTopping.ToppingId
                where drinkTopping.DrinkId == item.DrinkId
                    && storeTopping.StoreId == storeId
                    && toppingIds.Contains(drinkTopping.ToppingId)
                select new
                {
                    ToppingId = drinkTopping.ToppingId,
                    Name = drinkTopping.Topping.Name
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (toppingRows.Count != toppingIds.Length)
            {
                return Fail(
                    "Snapshot offline chứa topping không thuộc món hoặc cửa hàng gốc.",
                    POSCatalogSaleErrorCodes.ToppingInvalid);
            }


            var toppingRecipeIds = selected
                .Where(x => x.RecipeIdSnapshot.HasValue)
                .Select(x => x.RecipeIdSnapshot!.Value)
                .Distinct()
                .ToArray();
            var toppingRecipes = await _context.Recipes.AsNoTracking()
                .Where(x => toppingRecipeIds.Contains(x.RecipeId))
                .Select(x => new { x.RecipeId, x.ToppingId, x.DrinkId })
                .ToListAsync(cancellationToken);
            if (selected.Any(x =>
                (x.CostTreatment ?? ToppingCostTreatments.AddToppingRecipeCost) == ToppingCostTreatments.AddToppingRecipeCost
                && (!x.RecipeIdSnapshot.HasValue || !toppingRecipes.Any(r =>
                    r.RecipeId == x.RecipeIdSnapshot.Value
                    && r.ToppingId == x.ToppingId
                    && r.DrinkId == null))))
            {
                return Fail(
                    "Snapshot công thức topping không khớp topping gốc.",
                    POSCatalogSaleErrorCodes.ToppingInvalid);
            }

            var acceptedToppings = selected.Select(x => new POSAcceptedSaleToppingDto
            {
                ToppingId = x.ToppingId,
                Name = toppingRows.Single(y => y.ToppingId == x.ToppingId).Name,
                AcceptedPrice = Money(x.AcceptedPrice!.Value),
                QuantityPerDrink = x.QuantityPerDrink ?? 1m,
                QuantityUnit = x.QuantityUnit ?? ToppingQuantityUnits.RecipePortion,
                RecipeId = x.RecipeIdSnapshot,
                PriceTreatment = x.PriceTreatment ?? ToppingPriceTreatments.AddToppingPrice,
                CostTreatment = x.CostTreatment ?? ToppingCostTreatments.AddToppingRecipeCost
            }).ToList();
            var expectedUnitPrice = Money(item.AcceptedBasePrice!.Value + acceptedToppings.Sum(x => x.AcceptedPrice));
            if (Money(item.AcceptedUnitPrice!.Value) != expectedUnitPrice)
            {
                return Fail(
                    "Tổng tiền snapshot offline không khớp giá món và topping đã lưu.",
                    POSCatalogSaleErrorCodes.SnapshotInvalid);
            }

            return ServiceResult<POSAcceptedSaleLineDto>.Success(new POSAcceptedSaleLineDto
            {
                StoreMenuItemId = menuRow.StoreMenuItemId,
                DrinkSizeId = menuRow.DrinkSizeId,
                DrinkId = menuRow.DrinkSize.DrinkId,
                SizeId = menuRow.DrinkSize.SizeId,
                RecipeId = item.RecipeIdSnapshot!.Value,
                DrinkName = menuRow.DrinkSize.Drink.Name,
                SizeName = menuRow.DrinkSize.Size.Name,
                AcceptedBasePrice = Money(item.AcceptedBasePrice.Value),
                AcceptedUnitPrice = expectedUnitPrice,
                PriceSource = item.PriceSource!,
                CatalogVersion = item.CatalogVersion!.Value,
                Toppings = acceptedToppings
            });
        }

        private static ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>> ValidateOnlineToppings(
            POSOrderItemDto item,
            POSMenuItemDto menuItem,
            POSMenuItemSizeDto size)
        {
            var selected = item.Toppings ?? new List<POSOrderToppingDto>();
            if (selected.GroupBy(x => x.ToppingId).Any(x => x.Count() > 1))
            {
                return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Failure(
                    "Không được chọn trùng topping cho cùng một món.",
                    errorCode: POSCatalogSaleErrorCodes.ToppingInvalid);
            }

            var selectedIds = selected.Select(x => x.ToppingId).ToHashSet();
            var missingRequired = size.ToppingPolicies
                .Where(x => x.IsRequired)
                .FirstOrDefault(x => !selectedIds.Contains(x.ToppingId));
            if (missingRequired != null)
            {
                return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Failure(
                    "Món đang thiếu topping bắt buộc. Vui lòng làm mới lựa chọn.",
                    errorCode: POSCatalogSaleErrorCodes.ToppingInvalid);
            }

            var accepted = new List<POSAcceptedSaleToppingDto>();
            foreach (var toppingSnapshot in selected)
            {
                var topping = menuItem.AvailableToppings.SingleOrDefault(x => x.Id == toppingSnapshot.ToppingId);
                if (topping == null || !toppingSnapshot.AcceptedPrice.HasValue)
                {
                    return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Failure(
                        "Topping không còn khả dụng hoặc thiếu snapshot giá.",
                        errorCode: POSCatalogSaleErrorCodes.ToppingInvalid);
                }

                var policy = size.ToppingPolicies.SingleOrDefault(x => x.ToppingId == topping.Id);
                var costTreatment = policy?.CostTreatment ?? ToppingCostTreatments.AddToppingRecipeCost;
                var recipeId = policy?.RecipeId ?? topping.RecipeId;
                if (costTreatment == ToppingCostTreatments.AddToppingRecipeCost && !recipeId.HasValue)
                {
                    return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Failure(
                        $"Topping {topping.Name} chưa có công thức giá vốn hợp lệ.",
                        errorCode: POSCatalogSaleErrorCodes.ToppingInvalid);
                }
                var expectedPrice = policy?.PriceTreatment == ToppingPriceTreatments.IncludedInBasePrice
                    ? 0m
                    : Money(topping.Price * (policy?.QuantityPerDrink ?? 1m));
                if (Money(toppingSnapshot.AcceptedPrice.Value) != expectedPrice)
                {
                    return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Failure(
                        $"Giá topping {topping.Name} đã thay đổi. Vui lòng làm mới giỏ hàng.",
                        errorCode: POSCatalogSaleErrorCodes.SnapshotStale);
                }

                accepted.Add(new POSAcceptedSaleToppingDto
                {
                    ToppingId = topping.Id,
                    Name = topping.Name,
                    AcceptedPrice = expectedPrice,
                    QuantityPerDrink = policy?.QuantityPerDrink ?? 1m,
                    QuantityUnit = policy?.QuantityUnit ?? ToppingQuantityUnits.RecipePortion,
                    RecipeId = recipeId,
                    PriceTreatment = policy?.PriceTreatment ?? ToppingPriceTreatments.AddToppingPrice,
                    CostTreatment = costTreatment
                });
            }

            return ServiceResult<IReadOnlyList<POSAcceptedSaleToppingDto>>.Success(accepted);
        }

        private static bool IsValidOfflineToppingSnapshot(POSOrderToppingDto topping)
        {
            if (!topping.AcceptedPrice.HasValue || topping.AcceptedPrice.Value < 0)
                return false;

            var quantity = topping.QuantityPerDrink ?? 1m;
            var quantityUnit = topping.QuantityUnit ?? ToppingQuantityUnits.RecipePortion;
            var priceTreatment = topping.PriceTreatment ?? ToppingPriceTreatments.AddToppingPrice;
            var costTreatment = topping.CostTreatment ?? ToppingCostTreatments.AddToppingRecipeCost;
            return quantity > 0
                && ToppingQuantityUnits.All.Contains(quantityUnit)
                && ToppingPriceTreatments.All.Contains(priceTreatment)
                && (costTreatment == ToppingCostTreatments.IncludedInDrinkRecipe
                    || costTreatment == ToppingCostTreatments.AddToppingRecipeCost)
                && (costTreatment != ToppingCostTreatments.AddToppingRecipeCost
                    || topping.RecipeIdSnapshot.HasValue);
        }

        private static ServiceResult<POSAcceptedSaleLineDto>? ValidateRequiredSnapshot(POSOrderItemDto item)
        {
            if (item.Quantity <= 0)
                return Fail("Số lượng món phải lớn hơn 0.", POSCatalogSaleErrorCodes.SnapshotInvalid);
            if (!item.StoreMenuItemId.HasValue
                || !item.DrinkSizeId.HasValue
                || !item.RecipeIdSnapshot.HasValue
                || item.RecipeIdSnapshot.Value <= 0
                || !item.SizeId.HasValue
                || !item.AcceptedBasePrice.HasValue
                || !item.AcceptedUnitPrice.HasValue
                || !item.CatalogVersion.HasValue
                || string.IsNullOrWhiteSpace(item.PriceSource))
            {
                return Fail(
                    "Thiếu snapshot Store Menu cho món. Vui lòng làm mới catalog và giỏ hàng.",
                    POSCatalogSaleErrorCodes.SnapshotRequired);
            }

            return null;
        }

        private static ServiceResult<POSAcceptedSaleLineDto> Fail(string message, string errorCode) =>
            ServiceResult<POSAcceptedSaleLineDto>.Failure(message, errorCode: errorCode);

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
