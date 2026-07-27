using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.Services.Inventories;

public static class ProcurementPurchaseMath
{
    public static decimal CalculateLineTotal(
        PurchaseMode purchaseMode,
        decimal? packageCount,
        decimal? unitPricePerPackage,
        decimal? procurementQuantity,
        decimal? unitPricePerProcurementUnit)
    {
        return purchaseMode switch
        {
            PurchaseMode.Packaged when packageCount > 0m && unitPricePerPackage >= 0m =>
                decimal.Round(packageCount.Value * unitPricePerPackage.Value, 2, MidpointRounding.AwayFromZero),
            PurchaseMode.Loose when procurementQuantity > 0m && unitPricePerProcurementUnit >= 0m =>
                decimal.Round(procurementQuantity.Value * unitPricePerProcurementUnit.Value, 2, MidpointRounding.AwayFromZero),
            _ => throw new InvalidOperationException("Thông tin số lượng và đơn giá không phù hợp với hình thức mua.")
        };
    }

    public static bool IsWholePackageCount(decimal? packageCount) =>
        packageCount > 0m && decimal.Truncate(packageCount.Value) == packageCount.Value;

    public static decimal GetAcceptedProcurementQuantity(decimal received, decimal rejected)
    {
        if (received <= 0m)
            throw new InvalidOperationException("Số lượng thực nhận phải lớn hơn 0.");
        if (rejected < 0m || rejected > received)
            throw new InvalidOperationException("Số lượng bị loại phải từ 0 đến số lượng thực nhận.");

        return received - rejected;
    }
}
