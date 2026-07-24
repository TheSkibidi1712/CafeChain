using System.Data;
using System.Data.Common;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Dashboard;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<DashboardRepository> _logger;

    public DashboardRepository(AppDbContext context, ILogger<DashboardRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DashboardStoreOptionDto>> GetStoreOptionsAsync(
        IReadOnlyCollection<int> allowedStoreIds,
        CancellationToken cancellationToken = default)
    {
        if (allowedStoreIds.Count == 0) return [];
        return await _context.Stores.AsNoTracking()
            .Where(x => allowedStoreIds.Contains(x.StoreId) && x.Active)
            .OrderBy(x => x.Name)
            .Select(x => new DashboardStoreOptionDto
            {
                StoreId = x.StoreId,
                StoreName = x.Name,
                ProvinceId = x.ProvinceId,
                ProvinceName = x.Province != null ? x.Province.Name : string.Empty,
                DistrictId = x.DistrictId,
                DistrictName = x.District != null ? x.District.Name : string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public Task<ExecutiveDashboardData> GetExecutiveAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new ExecutiveDashboardData
        {
            NetSalesTrend = await QuerySafeAsync(connection, "dbo.usp_Dashboard_NetSalesTrend", filter, storeIds, MapNetSalesTrend, cancellationToken),
            StoreRanking = await QuerySafeAsync(connection, "dbo.usp_Dashboard_StoreRanking", filter, storeIds, MapStoreRanking, cancellationToken),
            PaymentMethodMix = await QuerySafeAsync(connection, "dbo.usp_Dashboard_PaymentMethodMix", filter, storeIds, MapPaymentMethodMix, cancellationToken),
            OrderHeatmap = await QuerySafeAsync(connection, "dbo.usp_Dashboard_OrderHeatmap", filter, storeIds, MapOrderHeatmap, cancellationToken),
            OperationalAlerts = await QuerySafeAsync(connection, "dbo.usp_Dashboard_OperationalAlerts", filter, storeIds, MapOperationalAlert, cancellationToken),
            OrderStatusSummary = await QuerySafeAsync(connection, "dbo.usp_Dashboard_OrderStatusSummary", filter, storeIds, MapOrderStatusSummary, cancellationToken)
        }, cancellationToken);

    public Task<OperationsDashboardData> GetOperationsAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new OperationsDashboardData
        {
            CashDiscrepancy = await QuerySafeAsync(connection, "dbo.usp_Operations_WorkShiftCashDiscrepancy", filter, storeIds, MapCashDiscrepancy, cancellationToken),
            ShiftSales = await QuerySafeAsync(connection, "dbo.usp_Operations_WorkShiftSales", filter, storeIds, MapShiftSales, cancellationToken),
            PaymentMix = await QuerySafeAsync(connection, "dbo.usp_Operations_WorkShiftPaymentMix", filter, storeIds, MapShiftPaymentMix, cancellationToken),
            OfflineReconciliation = await QuerySafeAsync(connection, "dbo.usp_Operations_OfflineReconciliationExceptions", filter, storeIds, MapOfflineReconciliation, cancellationToken),
            HourlyOrders = await QuerySafeAsync(connection, "dbo.usp_Operations_HourlyOrders", filter, storeIds, MapHourlyOrders, cancellationToken),
            TopDiscrepancies = await QuerySafeAsync(connection, "dbo.usp_Operations_WorkShiftTopDiscrepancies", filter, storeIds, MapTopDiscrepancy, cancellationToken),
            Kpis = await QuerySafeAsync(connection, "dbo.usp_Operations_WorkShiftKpis", filter, storeIds, MapShiftKpi, cancellationToken)
        }, cancellationToken);

    public Task<InventoryDashboardData> GetInventoryAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new InventoryDashboardData
        {
            ShortageRisk = await QuerySafeAsync(connection, "dbo.usp_Inventory_ShortageRisk", filter, storeIds, MapShortageRisk, cancellationToken),
            Movement = await QuerySafeAsync(connection, "dbo.usp_Inventory_MovementByType", filter, storeIds, MapInventoryMovement, cancellationToken),
            ThresholdRisk = await QuerySafeAsync(connection, "dbo.usp_Inventory_ThresholdRisk", filter, storeIds, MapThresholdRisk, cancellationToken),
            ReorderSuggestions = await QuerySafeAsync(connection, "dbo.usp_Inventory_ReorderSuggestions", filter, storeIds, MapReorder, cancellationToken),
            Waste = await QuerySafeAsync(connection, "dbo.usp_Inventory_WasteByStoreIngredient", filter, storeIds, MapInventoryWaste, cancellationToken),
            FifoAge = await QuerySafeAsync(connection, "dbo.usp_Inventory_FifoLayerAge", filter, storeIds, MapFifoAge, cancellationToken),
            IngredientConsumptionTrend = await QuerySafeAsync(connection, "dbo.usp_Inventory_IngredientConsumptionTrend", filter, storeIds, MapIngredientConsumptionTrend, cancellationToken)
        }, cancellationToken);

    public Task<ProcurementDashboardData> GetProcurementAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new ProcurementDashboardData
        {
            PurchaseOrderPipeline = await QuerySafeAsync(connection, "dbo.usp_Procurement_PurchaseOrderPipeline", filter, storeIds, MapPurchaseOrderPipeline, cancellationToken),
            OverduePurchaseOrders = await QuerySafeAsync(connection, "dbo.usp_Procurement_OverduePurchaseOrders", filter, storeIds, MapOverduePurchaseOrder, cancellationToken),
            SupplierQuality = await QuerySafeAsync(connection, "dbo.usp_Procurement_SupplierQuality", filter, storeIds, MapSupplierQuality, cancellationToken),
            PurchasePriceTrend = await QuerySafeAsync(connection, "dbo.usp_Procurement_PurchasePriceTrend", filter, storeIds, MapPurchasePriceTrend, cancellationToken),
            SpendBreakdown = await QuerySafeAsync(connection, "dbo.usp_Procurement_SpendBreakdown", filter, storeIds, MapProcurementSpend, cancellationToken),
            SupplierIssueMix = await QuerySafeAsync(connection, "dbo.usp_Procurement_SupplierIssueMix", filter, storeIds, MapSupplierIssueMix, cancellationToken)
        }, cancellationToken);

    public Task<ProductDashboardData> GetProductAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new ProductDashboardData
        {
            TopProducts = await QuerySafeAsync(connection, "dbo.usp_Product_TopProducts", filter, storeIds, MapTopProduct, cancellationToken),
            VolumeMargin = await QuerySafeAsync(connection, "dbo.usp_Product_VolumeMarginMatrix", filter, storeIds, MapVolumeMargin, cancellationToken),
            SizeMargin = await QuerySafeAsync(connection, "dbo.usp_Product_SizeMargin", filter, storeIds, MapSizeMargin, cancellationToken),
            TopToppings = await QuerySafeAsync(connection, "dbo.usp_Product_TopToppings", filter, storeIds, MapTopTopping, cancellationToken),
            BomHealth = await QuerySafeAsync(connection, "dbo.usp_Product_BomHealth", filter, storeIds, MapBomHealth, cancellationToken),
            LowEfficiency = await QuerySafeAsync(connection, "dbo.usp_Product_HighConsumptionLowEfficiency", filter, storeIds, MapLowEfficiency, cancellationToken),
            CategoryPerformance = await QuerySafeAsync(connection, "dbo.usp_Product_CategoryPerformance", filter, storeIds, MapCategoryPerformance, cancellationToken),
            ProductPeriodPerformance = await QuerySafeAsync(connection, "dbo.usp_Product_PeriodPerformance", filter, storeIds, MapProductPeriodPerformance, cancellationToken)
        }, cancellationToken);

    public Task<WorkforceDashboardData> GetWorkforceAsync(
        DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection => new WorkforceDashboardData
        {
            ShiftStatus = await QuerySafeAsync(connection, "dbo.usp_Workforce_ShiftStatus", filter, storeIds, MapWorkforceShiftStatus, cancellationToken),
            HourlyDemand = await QuerySafeAsync(connection, "dbo.usp_Workforce_HourlyDemand", filter, storeIds, MapWorkforceHourlyDemand, cancellationToken),
            StaffPerformance = await QuerySafeAsync(connection, "dbo.usp_Workforce_StaffPerformance", filter, storeIds, MapWorkforceStaffPerformance, cancellationToken)
        }, cancellationToken);

    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken);
        try { return await action(connection); }
        finally { if (close) await connection.CloseAsync(); }
    }

    private async Task<DashboardWidgetResult<T>> QuerySafeAsync<T>(
        DbConnection connection,
        string procedure,
        DashboardFilterDto filter,
        IReadOnlyCollection<int> storeIds,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = procedure;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;
            AddParameter(command, "@FromDate", DbType.Date, filter.FromDate.Date);
            AddParameter(command, "@ToDate", DbType.Date, filter.ToDate.Date);
            AddParameter(command, "@StoreIds", DbType.String, string.Join(',', storeIds.OrderBy(x => x)));
            AddParameter(command, "@Granularity", DbType.String, filter.Granularity);
            AddParameter(command, "@Top", DbType.Int32, filter.Top);

            var rows = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(map(reader));
            return DashboardWidgetResult<T>.Success(rows);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is DbException or InvalidCastException or IndexOutOfRangeException or FormatException or OverflowException)
        {
            _logger.LogWarning(exception, "Dashboard procedure failed. Procedure={Procedure}", procedure);
            return DashboardWidgetResult<T>.Failure("Không thể tải widget này. Vui lòng thử lại.");
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static NetSalesTrendRow MapNetSalesTrend(DbDataReader r) => new() { BucketDate = Date(r,"BucketDate"), TotalOrders = Long(r,"TotalOrders"), NetSales = Decimal(r,"NetSales"), DataStatus = String(r,"DataStatus") };
    private static StoreRankingRow MapStoreRanking(DbDataReader r) => new() { StoreId = Int(r,"StoreId"), StoreName = String(r,"StoreName"), TotalOrders = Long(r,"TotalOrders"), NetSales = Decimal(r,"NetSales"), AverageOrderValue = Decimal(r,"AverageOrderValue"), DataStatus = String(r,"DataStatus") };
    private static PaymentMethodMixRow MapPaymentMethodMix(DbDataReader r) => new() { PaymentMethodId = Int(r,"PaymentMethodId"), PaymentMethodCode = String(r,"PaymentMethodCode"), PaymentMethodName = String(r,"PaymentMethodName"), TotalTransactions = Long(r,"TotalTransactions"), Amount = Decimal(r,"Amount"), Share = Decimal(r,"Share"), DataStatus = String(r,"DataStatus") };
    private static OrderHeatmapRow MapOrderHeatmap(DbDataReader r) => new() { IsoWeekday = Int(r,"IsoWeekday"), HourOfDay = Int(r,"HourOfDay"), TotalOrders = Long(r,"TotalOrders"), NetSales = Decimal(r,"NetSales"), DataStatus = String(r,"DataStatus") };
    private static OperationalAlertRow MapOperationalAlert(DbDataReader r) => new() { AlertType = String(r,"AlertType"), StoreId = Int(r,"StoreId"), EntityId = Int(r,"EntityId"), Severity = String(r,"Severity"), AlertValue = Decimal(r,"AlertValue"), Message = String(r,"Message"), DataStatus = String(r,"DataStatus") };
    private static OrderStatusSummaryRow MapOrderStatusSummary(DbDataReader r) => new() { StoreId=Int(r,"StoreId"),StoreName=String(r,"StoreName"),TotalOrders=Long(r,"TotalOrders"),CompletedOrders=Long(r,"CompletedOrders"),CancelledOrders=Long(r,"CancelledOrders"),CancellationRate=Decimal(r,"CancellationRate"),DataStatus=String(r,"DataStatus") };
    private static WorkShiftCashDiscrepancyRow MapCashDiscrepancy(DbDataReader r) => new() { WorkShiftId=Int(r,"WorkShiftId"),StoreId=Int(r,"StoreId"),StoreName=String(r,"StoreName"),StaffId=Int(r,"StaffId"),FullName=String(r,"FullName"),StartTime=Date(r,"StartTime"),EndTime=NullableDate(r,"EndTime"),StartingCash=Decimal(r,"StartingCash"),ExpectedEndingCash=NullableDecimal(r,"ExpectedEndingCash"),ActualEndingCash=NullableDecimal(r,"ActualEndingCash"),CashDiscrepancy=NullableDecimal(r,"CashDiscrepancy"),DiscrepancyReason=NullableString(r,"DiscrepancyReason"),IsExceptionClosed=Bool(r,"IsExceptionClosed"),RequiresReconciliation=Bool(r,"RequiresReconciliation"),DataStatus=String(r,"DataStatus") };
    private static WorkShiftSalesRow MapShiftSales(DbDataReader r) => new() { WorkShiftId=Int(r,"WorkShiftId"),StoreId=Int(r,"StoreId"),TotalOrders=Long(r,"TotalOrders"),NetSales=Decimal(r,"NetSales"),AverageOrderValue=Decimal(r,"AverageOrderValue"),DataStatus=String(r,"DataStatus") };
    private static WorkShiftPaymentMixRow MapShiftPaymentMix(DbDataReader r) => new() { WorkShiftId=Int(r,"WorkShiftId"),StoreId=Int(r,"StoreId"),PaymentMethodId=Int(r,"PaymentMethodId"),PaymentMethodCode=String(r,"PaymentMethodCode"),PaymentMethodName=String(r,"PaymentMethodName"),TotalTransactions=Long(r,"TotalTransactions"),Amount=Decimal(r,"Amount"),DataStatus=String(r,"DataStatus") };
    private static OfflineReconciliationRow MapOfflineReconciliation(DbDataReader r) => new() { WorkShiftId=Int(r,"WorkShiftId"),StoreId=Int(r,"StoreId"),IsExceptionClosed=Bool(r,"IsExceptionClosed"),OfflineOrderCountAtClose=Int(r,"OfflineOrderCountAtClose"),OfflineEstimatedTotalAtClose=Decimal(r,"OfflineEstimatedTotalAtClose"),OfflineCashTotalAtClose=Decimal(r,"OfflineCashTotalAtClose"),RequiresReconciliation=Bool(r,"RequiresReconciliation"),HasLateOfflineSync=Bool(r,"HasLateOfflineSync"),LateOfflineSyncCount=Int(r,"LateOfflineSyncCount"),LastLateOfflineSyncedAt=NullableDate(r,"LastLateOfflineSyncedAt"),DataStatus=String(r,"DataStatus") };
    private static HourlyOrdersRow MapHourlyOrders(DbDataReader r) => new() { HourOfDay=Int(r,"HourOfDay"),TotalOrders=Long(r,"TotalOrders"),NetSales=Decimal(r,"NetSales"),DataStatus=String(r,"DataStatus") };
    private static WorkShiftTopDiscrepancyRow MapTopDiscrepancy(DbDataReader r) => new() { WorkShiftId=Int(r,"WorkShiftId"),StoreId=Int(r,"StoreId"),StaffId=Int(r,"StaffId"),CashDiscrepancy=NullableDecimal(r,"CashDiscrepancy"),AbsoluteDiscrepancy=Decimal(r,"AbsoluteDiscrepancy"),DiscrepancyReason=NullableString(r,"DiscrepancyReason"),EndTime=Date(r,"EndTime"),DataStatus=String(r,"DataStatus") };
    private static WorkShiftKpiRow MapShiftKpi(DbDataReader r) => new() { TotalWorkShifts=Long(r,"TotalWorkShifts"),OpenWorkShifts=Int(r,"OpenWorkShifts"),ExceptionClosedCount=Int(r,"ExceptionClosedCount"),ReconciliationCount=Int(r,"ReconciliationCount"),AbsoluteCashDiscrepancy=Decimal(r,"AbsoluteCashDiscrepancy"),DataStatus=String(r,"DataStatus") };
    private static InventoryShortageRiskRow MapShortageRisk(DbDataReader r) => new() { StoreInventoryId=Int(r,"StoreInventoryId"),StoreId=Int(r,"StoreId"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),AvailableQty=Decimal(r,"AvailableQty"),ReservedQty=Decimal(r,"ReservedQty"),MinStockLevel=NullableDecimal(r,"MinStockLevel"),RiskLevel=String(r,"RiskLevel"),DataStatus=String(r,"DataStatus") };
    private static InventoryMovementRow MapInventoryMovement(DbDataReader r) => new() { MovementDate=Date(r,"MovementDate"),TransactionType=Int(r,"TransactionType"),TransactionCount=Long(r,"TransactionCount"),Quantity=Decimal(r,"Quantity"),TotalCost=Decimal(r,"TotalCost"),DataStatus=String(r,"DataStatus") };
    private static InventoryThresholdRiskRow MapThresholdRisk(DbDataReader r) => new() { StoreInventoryId=Int(r,"StoreInventoryId"),StoreId=Int(r,"StoreId"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),AvailableQty=Decimal(r,"AvailableQty"),ReservedQty=Decimal(r,"ReservedQty"),MinStockLevel=NullableDecimal(r,"MinStockLevel"),MaxNegativeQty=Decimal(r,"MaxNegativeQty"),QuantityAboveMinimum=Decimal(r,"QuantityAboveMinimum"),DataStatus=String(r,"DataStatus") };
    private static InventoryReorderRow MapReorder(DbDataReader r) => new() { RestockRequestId=Int(r,"RestockRequestId"),StoreId=Int(r,"StoreId"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),RequestedQuantity=Decimal(r,"RequestedQuantity"),SuggestedQuantity=NullableDecimal(r,"SuggestedQuantity"),SuggestionAverageDailyUsageSnapshot=NullableDecimal(r,"SuggestionAverageDailyUsageSnapshot"),SuggestionLeadTimeDaysSnapshot=NullableInt(r,"SuggestionLeadTimeDaysSnapshot"),SuggestionIncomingQuantitySnapshot=NullableDecimal(r,"SuggestionIncomingQuantitySnapshot"),SuggestionReason=NullableString(r,"SuggestionReason"),Status=String(r,"Status"),Priority=String(r,"Priority"),CreatedAt=Date(r,"CreatedAt"),DataStatus=String(r,"DataStatus") };
    private static InventoryWasteRow MapInventoryWaste(DbDataReader r) => new() { StoreId=Int(r,"StoreId"),StoreName=String(r,"StoreName"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),WasteQuantity=Decimal(r,"WasteQuantity"),WasteValue=Decimal(r,"WasteValue"),TransactionCount=Long(r,"TransactionCount"),DataStatus=String(r,"DataStatus") };
    private static InventoryFifoAgeRow MapFifoAge(DbDataReader r) => new() { InventoryCostLayerId=Long(r,"InventoryCostLayerId"),StoreId=Int(r,"StoreId"),IngredientId=NullableInt(r,"IngredientId"),PreparedItemId=NullableInt(r,"PreparedItemId"),RemainingQuantity=Decimal(r,"RemainingQuantity"),UnitCost=Decimal(r,"UnitCost"),CreatedAt=Date(r,"CreatedAt"),AgeDays=Int(r,"AgeDays"),RemainingValue=Decimal(r,"RemainingValue"),DataStatus=String(r,"DataStatus") };
    private static IngredientConsumptionTrendRow MapIngredientConsumptionTrend(DbDataReader r) => new() { BucketDate=Date(r,"BucketDate"),StoreId=Int(r,"StoreId"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),ConsumedQuantity=Decimal(r,"ConsumedQuantity"),ConfirmedCost=Decimal(r,"ConfirmedCost"),TransactionCount=Long(r,"TransactionCount"),DataStatus=String(r,"DataStatus") };
    private static PurchaseOrderPipelineRow MapPurchaseOrderPipeline(DbDataReader r) => new() { Status=String(r,"Status"),PurchaseOrderCount=Long(r,"PurchaseOrderCount"),OrderedValue=Decimal(r,"OrderedValue"),DataStatus=String(r,"DataStatus") };
    private static OverduePurchaseOrderRow MapOverduePurchaseOrder(DbDataReader r) => new() { PurchaseOrderId=Int(r,"PurchaseOrderId"),Code=String(r,"Code"),StoreId=Int(r,"StoreId"),SupplierId=Int(r,"SupplierId"),SupplierName=String(r,"SupplierName"),Status=String(r,"Status"),OrderDate=Date(r,"OrderDate"),ExpectedDeliveryAtUtc=Date(r,"ExpectedDeliveryAtUtc"),OverdueDays=Int(r,"OverdueDays"),DataStatus=String(r,"DataStatus") };
    private static SupplierQualityRow MapSupplierQuality(DbDataReader r) => new() { SupplierId=Int(r,"SupplierId"),SupplierName=String(r,"SupplierName"),AcceptedBaseQuantity=Decimal(r,"AcceptedBaseQuantity"),RejectedBaseQuantity=Decimal(r,"RejectedBaseQuantity"),RejectionRate=Decimal(r,"RejectionRate"),ReceiptCount=Long(r,"ReceiptCount"),DataStatus=String(r,"DataStatus") };
    private static PurchasePriceTrendRow MapPurchasePriceTrend(DbDataReader r) => new() { ReceiptDate=Date(r,"ReceiptDate"),IngredientId=Int(r,"IngredientId"),IngredientName=String(r,"IngredientName"),AverageBaseUnitCost=Decimal(r,"AverageBaseUnitCost"),MinimumBaseUnitCost=Decimal(r,"MinimumBaseUnitCost"),MaximumBaseUnitCost=Decimal(r,"MaximumBaseUnitCost"),ReceivedBaseQuantity=Decimal(r,"ReceivedBaseQuantity"),DataStatus=String(r,"DataStatus") };
    private static ProcurementSpendRow MapProcurementSpend(DbDataReader r) => new() { SupplierId=Int(r,"SupplierId"),SupplierName=String(r,"SupplierName"),StoreId=Int(r,"StoreId"),Spend=Decimal(r,"Spend"),ReceiptCount=Long(r,"ReceiptCount"),DataStatus=String(r,"DataStatus") };
    private static SupplierIssueMixRow MapSupplierIssueMix(DbDataReader r) => new() { IssueType=String(r,"IssueType"),Status=String(r,"Status"),IssueCount=Long(r,"IssueCount"),AffectedBaseQuantity=Decimal(r,"AffectedBaseQuantity"),DataStatus=String(r,"DataStatus") };
    private static TopProductRow MapTopProduct(DbDataReader r) => new() { DrinkId=Int(r,"DrinkId"),DrinkName=String(r,"DrinkName"),TotalSold=Int(r,"TotalSold"),ProductRevenue=Decimal(r,"ProductRevenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedGrossProfit=Decimal(r,"ConfirmedGrossProfit"),DataStatus=String(r,"DataStatus") };
    private static VolumeMarginRow MapVolumeMargin(DbDataReader r) => new() { DrinkId=Int(r,"DrinkId"),DrinkName=String(r,"DrinkName"),Volume=Int(r,"Volume"),Revenue=Decimal(r,"Revenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedMarginRate=Decimal(r,"ConfirmedMarginRate"),DataStatus=String(r,"DataStatus") };
    private static SizeMarginRow MapSizeMargin(DbDataReader r) => new() { SizeId=NullableInt(r,"SizeId"),SizeName=String(r,"SizeName"),TotalSold=Int(r,"TotalSold"),Revenue=Decimal(r,"Revenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedGrossProfit=Decimal(r,"ConfirmedGrossProfit"),DataStatus=String(r,"DataStatus") };
    private static TopToppingAnalyticsRow MapTopTopping(DbDataReader r) => new() { ToppingId=Int(r,"ToppingId"),ToppingName=String(r,"ToppingName"),TotalUsed=Int(r,"TotalUsed"),Revenue=Decimal(r,"Revenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),DataStatus=String(r,"DataStatus") };
    private static BomHealthRow MapBomHealth(DbDataReader r) => new() { DrinkId=Int(r,"DrinkId"),DrinkCode=String(r,"DrinkCode"),DrinkName=String(r,"DrinkName"),RecipeCount=Int(r,"RecipeCount"),RecipeLineCount=Int(r,"RecipeLineCount"),InvalidLineCount=Int(r,"InvalidLineCount"),DataStatus=String(r,"DataStatus") };
    private static LowEfficiencyProductRow MapLowEfficiency(DbDataReader r) => new() { DrinkId=Int(r,"DrinkId"),DrinkName=String(r,"DrinkName"),TotalSold=Int(r,"TotalSold"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedGrossProfit=Decimal(r,"ConfirmedGrossProfit"),DataStatus=String(r,"DataStatus") };
    private static CategoryPerformanceRow MapCategoryPerformance(DbDataReader r) => new() { CategoryId=NullableInt(r,"CategoryId"),CategoryName=String(r,"CategoryName"),TotalSold=Int(r,"TotalSold"),Revenue=Decimal(r,"Revenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedGrossProfit=Decimal(r,"ConfirmedGrossProfit"),ConfirmedMarginRate=Decimal(r,"ConfirmedMarginRate"),DataStatus=String(r,"DataStatus") };
    private static ProductPeriodPerformanceRow MapProductPeriodPerformance(DbDataReader r) => new() { DrinkId=Int(r,"DrinkId"),DrinkName=String(r,"DrinkName"),TotalSold=Int(r,"TotalSold"),Revenue=Decimal(r,"Revenue"),ConfirmedCogs=Decimal(r,"ConfirmedCogs"),ConfirmedGrossProfit=Decimal(r,"ConfirmedGrossProfit"),ConfirmedMarginRate=Decimal(r,"ConfirmedMarginRate"),DataStatus=String(r,"DataStatus") };
    private static WorkforceShiftStatusRow MapWorkforceShiftStatus(DbDataReader r) => new() { StaffShiftId=Int(r,"StaffShiftId"),StaffId=Int(r,"StaffId"),FullName=String(r,"FullName"),StoreId=Int(r,"StoreId"),WorkDate=Date(r,"WorkDate"),ShiftId=Int(r,"ShiftId"),ShiftName=String(r,"ShiftName"),PlannedStartAt=Date(r,"PlannedStartAt"),PlannedEndAt=Date(r,"PlannedEndAt"),StatusCode=String(r,"StatusCode"),IsOvernight=Bool(r,"IsOvernight"),DataStatus=String(r,"DataStatus") };
    private static WorkforceHourlyDemandRow MapWorkforceHourlyDemand(DbDataReader r) => new() { HourOfDay=Int(r,"HourOfDay"),TotalOrders=Long(r,"TotalOrders"),ScheduledStaffCount=Long(r,"ScheduledStaffCount"),DataStatus=String(r,"DataStatus") };
    private static WorkforceStaffPerformanceRow MapWorkforceStaffPerformance(DbDataReader r) => new() { StaffId=Int(r,"StaffId"),FullName=String(r,"FullName"),StoreId=Int(r,"StoreId"),WorkShiftCount=Long(r,"WorkShiftCount"),TotalOrders=Long(r,"TotalOrders"),NetSales=Decimal(r,"NetSales"),AverageOrderValue=Decimal(r,"AverageOrderValue"),OrdersPerWorkShift=Decimal(r,"OrdersPerWorkShift"),DataStatus=String(r,"DataStatus") };

    private static int Ordinal(DbDataReader r, string name) => r.GetOrdinal(name);
    private static bool IsNull(DbDataReader r, string name) => r.IsDBNull(Ordinal(r, name));
    private static string String(DbDataReader r, string name) => IsNull(r,name) ? string.Empty : Convert.ToString(r.GetValue(Ordinal(r,name))) ?? string.Empty;
    private static string? NullableString(DbDataReader r, string name) => IsNull(r,name) ? null : Convert.ToString(r.GetValue(Ordinal(r,name)));
    private static int Int(DbDataReader r, string name) => IsNull(r,name) ? 0 : Convert.ToInt32(r.GetValue(Ordinal(r,name)));
    private static int? NullableInt(DbDataReader r, string name) => IsNull(r,name) ? null : Convert.ToInt32(r.GetValue(Ordinal(r,name)));
    private static long Long(DbDataReader r, string name) => IsNull(r,name) ? 0L : Convert.ToInt64(r.GetValue(Ordinal(r,name)));
    private static decimal Decimal(DbDataReader r, string name) => IsNull(r,name) ? 0m : Convert.ToDecimal(r.GetValue(Ordinal(r,name)));
    private static decimal? NullableDecimal(DbDataReader r, string name) => IsNull(r,name) ? null : Convert.ToDecimal(r.GetValue(Ordinal(r,name)));
    private static bool Bool(DbDataReader r, string name) => !IsNull(r,name) && Convert.ToBoolean(r.GetValue(Ordinal(r,name)));
    private static DateTime Date(DbDataReader r, string name) => IsNull(r,name) ? default : Convert.ToDateTime(r.GetValue(Ordinal(r,name)));
    private static DateTime? NullableDate(DbDataReader r, string name) => IsNull(r,name) ? null : Convert.ToDateTime(r.GetValue(Ordinal(r,name)));
}
