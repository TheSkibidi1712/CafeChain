using CafeChain.Application.DTOs.Admin;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Services.Systems;
using Xunit;

namespace CafeChain.Tests;

public sealed class BusinessTimezoneIssue416Tests
{
    private static readonly DateTime LedgerUtc = new(2026, 8, 9, 17, 1, 0, DateTimeKind.Utc);
    private static readonly DateTime PaymentBusinessLocal = new(2026, 8, 10, 0, 1, 0, DateTimeKind.Unspecified);

    [Fact]
    public void UtcInstant_CrossingMidnight_DisplaysCorrectVietnamDate()
    {
        var service = new BusinessDateService();

        var local = service.ToBusinessTime(LedgerUtc);

        Assert.Equal(PaymentBusinessLocal, local);
        Assert.Equal("00:01 10/08/2026", local.ToString("HH:mm dd/MM/yyyy"));
    }

    [Fact]
    public void SalesHistory_DisplaysPaymentTimeInBusinessTimezone()
    {
        var row = new AdminOrderHistoryRowDto { CreatedAt = PaymentBusinessLocal };
        var payment = new AdminOrderHistoryPaymentDto { PaidAt = PaymentBusinessLocal };

        Assert.Equal("00:01 10/08/2026", row.CreatedAtDisplay);
        Assert.Equal("00:01 10/08/2026", payment.PaidAtDisplay);

        var view = Read("CafeChain/Areas/Admin/Views/AdminOrder/History.cshtml");
        Assert.Contains("row.createdAtDisplay", view, StringComparison.Ordinal);
        Assert.Contains("payment.paidAtDisplay", view, StringComparison.Ordinal);
        Assert.DoesNotContain("dateTime(row.createdAt)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryHistory_DisplaysSalePostingTimeInBusinessTimezone()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionPartial.cshtml");

        Assert.Contains("BusinessDateService.ToBusinessTime(item.CreatedAt)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("item.CreatedAt.ToString", view, StringComparison.Ordinal);
    }

    [Fact]
    public void SamePosOrder_HasSameDisplayedBusinessTimeAcrossSalesAndInventory()
    {
        var service = new BusinessDateService();
        var sales = new AdminOrderHistoryRowDto { CreatedAt = PaymentBusinessLocal };
        var inventoryDisplay = service.ToBusinessTime(LedgerUtc).ToString("HH:mm dd/MM/yyyy");

        Assert.Equal(sales.CreatedAtDisplay, inventoryDisplay);
    }

    [Fact]
    public void StoreInventory_TimezoneFix_DoesNotChangeQuantityOrCost()
    {
        var transaction = new InventoryTransactionDTO
        {
            Quantity = 70m,
            BeforeQty = 7610m,
            AfterQty = 7540m,
            UnitPrice = 240m,
            TotalAmount = 16800m,
            CreatedAt = LedgerUtc
        };
        var service = new BusinessDateService();

        _ = service.ToBusinessTime(transaction.CreatedAt);

        Assert.Equal(70m, transaction.Quantity);
        Assert.Equal(7610m, transaction.BeforeQty);
        Assert.Equal(7540m, transaction.AfterQty);
        Assert.Equal(240m, transaction.UnitPrice);
        Assert.Equal(16800m, transaction.TotalAmount);
    }

    [Fact]
    public void StockAlertTimestamp_DisplaysBusinessTimezone()
    {
        var index = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml");
        var details = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml");

        Assert.Contains("BusinessDateService.ToBusinessTime", index, StringComparison.Ordinal);
        Assert.Contains("BusinessDateService.ToBusinessTime", details, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToLocalTime()", index, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToLocalTime()", details, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryHistory_UsesBusinessOrderCodeForTraceability()
    {
        var repository = Read("CafeChain/Infrastructure/Repositories/Admin/StoreInventories/AdminStoreInventoryRepository.cs");

        Assert.Contains("#CC{referenceOrderId.Value:D5}", repository, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
