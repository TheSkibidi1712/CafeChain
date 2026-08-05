using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// Service xử lý nghiệp vụ POS Order
    /// Inject IPOSOrderRepository thay vì AppDbContext — đúng pattern Repository
    /// </summary>
    public class POSOrderService : IPOSOrderService
    {
        private readonly IPOSOrderRepository _repository;
        private readonly IWorkShiftService _workShiftService;
        private readonly IAdminVoucherService _voucherService;
        private readonly IPrintDispatcher _printDispatcher;
        private readonly IPayOSService _payOSService;
        private readonly ILogger<POSOrderService> _logger;
        private readonly IPOSStoreMenuSaleValidator? _storeMenuSaleValidator;
        private readonly IOrderAccessAuthorizationService? _orderAccessAuthorization;
        private readonly IPOSIceCustomizationService? _iceCustomization;
        private readonly decimal _cashDenominationStep;

        public POSOrderService(
            IPOSOrderRepository repository,
            IWorkShiftService workShiftService,
            IAdminVoucherService voucherService,
            IPrintDispatcher printDispatcher,
            IPayOSService payOSService,
            ILogger<POSOrderService> logger)
            : this(repository, workShiftService, voucherService, printDispatcher, payOSService, logger, null, null, null)
        {
        }

        public POSOrderService(
            IPOSOrderRepository repository,
            IWorkShiftService workShiftService,
            IAdminVoucherService voucherService,
            IPrintDispatcher printDispatcher,
            IPayOSService payOSService,
            ILogger<POSOrderService> logger,
            IPOSStoreMenuSaleValidator? storeMenuSaleValidator)
            : this(repository, workShiftService, voucherService, printDispatcher, payOSService, logger, storeMenuSaleValidator, null, null)
        {
        }

        public POSOrderService(
            IPOSOrderRepository repository,
            IWorkShiftService workShiftService,
            IAdminVoucherService voucherService,
            IPrintDispatcher printDispatcher,
            IPayOSService payOSService,
            ILogger<POSOrderService> logger,
            IPOSStoreMenuSaleValidator? storeMenuSaleValidator,
            IOptions<POSPaymentOptions>? paymentOptions,
            IOrderAccessAuthorizationService? orderAccessAuthorization = null,
            IPOSIceCustomizationService? iceCustomization = null)
        {
            _repository = repository;
            _workShiftService = workShiftService;
            _voucherService = voucherService;
            _printDispatcher = printDispatcher;
            _payOSService = payOSService;
            _logger = logger;
            _storeMenuSaleValidator = storeMenuSaleValidator;
            _orderAccessAuthorization = orderAccessAuthorization;
            _iceCustomization = iceCustomization;
            _cashDenominationStep = paymentOptions?.Value.GetEffectiveCashDenominationStep()
                ?? POSPaymentOptions.DefaultCashDenominationStep;
        }

        // ============================================================
        // GET MENU DATA
        // ============================================================
        public async Task<ServiceResult<object>> GetMenuDataAsync(int storeId)
        {
            var storeDrinkIds = await _repository.GetStoreDrinkIdsAsync(storeId);
            var categories = await _repository.GetCategoriesWithDrinksAsync(storeDrinkIds);
            var storeToppings = await _repository.GetStoreToppingsAsync(storeId);

            return ServiceResult<object>.Success(new { categories, storeToppings } as object);
        }

        // ============================================================
        // SEARCH CUSTOMER
        // ============================================================
        public async Task<ServiceResult<object>> SearchCustomerAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return ServiceResult<object>.Failure("Vui lòng nhập số điện thoại.");

            var customer = await _repository.SearchCustomerByPhoneAsync(phone);
            if (customer == null)
                return ServiceResult<object>.Failure("Không tìm thấy khách hàng.");

            return ServiceResult<object>.Success(customer);
        }

        // ============================================================
        // REGISTER CUSTOMER — Quick registration from POS
        // ============================================================
        public async Task<ServiceResult<object>> RegisterCustomerAsync(QuickCustomerRegisterDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.FullName))
                    return ServiceResult<object>.Failure("Số điện thoại và họ tên là bắt buộc.");

                // Kiểm tra trùng SĐT
                var isDuplicate = await _repository.HasDuplicatePhoneAsync(dto.Phone);
                if (isDuplicate)
                    return ServiceResult<object>.Failure("Số điện thoại này đã được đăng ký trong hệ thống.");

                // Tạo Customer mới
                var newCustomer = new Customer
                {
                    CustomerCode = "KH" + DateTime.Now.Ticks,
                    FullName = dto.FullName,
                    MemberLevelId = 1, // New Member
                    CurrentPoints = 0,
                    TotalSpent = 0,
                    TotalOrders = 0,
                    Active = true,
                    CreatedAt = DateTime.Now,
                    DateOfBirth = dto.DateOfBirth
                };

                var newPhone = new CustomerPhone
                {
                    Phone = dto.Phone,
                    IsDefault = true
                };

                var customer = await _repository.RegisterCustomerAsync(newCustomer, newPhone);

                return ServiceResult<object>.Success(new
                {
                    customerId = customer.CustomerId,
                    fullName = customer.FullName,
                    phone = dto.Phone,
                    currentPoints = 0,
                    memberLevel = "Thành viên mới"
                } as object, "Đăng ký khách hàng thành công!");
            }
            catch (Exception ex)
            {
                return ServiceResult<object>.Failure("Lỗi hệ thống khi đăng ký: " + ex.Message);
            }
        }

        // ============================================================
        // COMMIT ORDER — Core business logic
        // ============================================================
        public async Task<ServiceResult<object>> CommitOrderAsync(POSOrderCommitDto dto, int userId, int storeId)
        {
            // ── ADR-0002: IDEMPOTENCY CHECK — trước khi bắt đầu transaction ──
            // Nếu ClientOrderId đã tồn tại → trả order cũ (200), không tạo duplicate
            if (dto.ClientOrderId.HasValue)
            {
                var existingOrder = await _repository.FindOrderByClientOrderIdAsync(
                    dto.ClientOrderId.Value,
                    storeId);
                if (existingOrder != null)
                {
                    if (!IsCompatibleIdempotentReplay(existingOrder, dto))
                    {
                        return ServiceResult<object>.Failure(
                            "ClientOrderId đã được dùng với nội dung thanh toán khác.",
                            errorCode: "IDEMPOTENCY_KEY_REUSED");
                    }

                    _logger.LogInformation(
                        "[CommitOrder] Idempotent — Order #{OrderId} đã tồn tại cho ClientOrderId={ClientOrderId}",
                        existingOrder.OrderId, dto.ClientOrderId);

                    var effectiveReceivedAmount = ResolveCashReceivedAmount(dto.ReceivedAmount, existingOrder.Total);
                    var idempotentChange = effectiveReceivedAmount - existingOrder.Total;
                    var isExistingPayOsOrder = existingOrder.Payments?.Any(p => p.PaymentMethodId == 2) == true
                        && existingOrder.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid;

                    if (isExistingPayOsOrder)
                    {
                        PayOSCreateLinkResult existingPaymentLink;
                        var pendingCashAmount = existingOrder.Payments?
                            .Where(p => p.PaymentMethodId == 1 && p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                            .Sum(p => p.Amount) ?? 0m;
                        var pendingVietQrAmount = existingOrder.Payments?
                            .Where(p => p.PaymentMethodId == 2 && p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
                            .Sum(p => p.Amount) ?? existingOrder.Total;
                        try
                        {
                            existingPaymentLink = pendingVietQrAmount == existingOrder.Total
                                ? await _payOSService.CreatePaymentLinkAsync(existingOrder.OrderId)
                                : await _payOSService.CreatePaymentLinkAsync(existingOrder.OrderId, pendingVietQrAmount);
                        }
                        catch (Exception payOsEx)
                        {
                            _logger.LogError(
                                payOsEx,
                                "[CommitOrder] Không thể tạo lại PayOS link cho Order #{OrderId}.",
                                existingOrder.OrderId);
                            return ServiceResult<object>.Failure("Không thể tạo mã thanh toán VietQR: " + payOsEx.Message);
                        }

                        return ServiceResult<object>.Success(new
                        {
                            orderId = existingOrder.OrderId,
                            clientOrderId = existingOrder.ClientOrderId?.ToString(),
                            subTotal = existingOrder.SubTotal,
                            voucherDiscount = existingOrder.VoucherDiscount,
                            pointDiscount = existingOrder.PointDiscount,
                            total = existingOrder.Total,
                            receivedAmount = effectiveReceivedAmount,
                            changeAmount = 0m,
                            earnedPoints = 0,
                            isIdempotent = true,
                            requiresPayment = true,
                            paymentMethodId = 2,
                            pendingCashAmount,
                            pendingVietQrAmount,
                            checkoutUrl = existingPaymentLink.CheckoutUrl,
                            qrCode = existingPaymentLink.QrCode,
                            orderCode = existingPaymentLink.OrderCode
                        } as object, "Đơn chuyển khoản đã tồn tại, tiếp tục chờ thanh toán.");
                    }

                    return ServiceResult<object>.Success(new
                    {
                        orderId = existingOrder.OrderId,
                        clientOrderId = existingOrder.ClientOrderId?.ToString(),
                        subTotal = existingOrder.SubTotal,
                        voucherDiscount = existingOrder.VoucherDiscount,
                        pointDiscount = existingOrder.PointDiscount,
                        total = existingOrder.Total,
                        receivedAmount = effectiveReceivedAmount,
                        changeAmount = idempotentChange > 0 ? idempotentChange : 0m,
                        earnedPoints = 0,  // Không tính lại điểm — order cũ đã xử lý
                        isIdempotent = true
                    } as object, "Đơn hàng đã tồn tại (idempotent).");
                }
            }

            var activeShift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (activeShift == null)
                return ServiceResult<object>.Failure("Phiên két tiền đã đóng, vui lòng mở ca mới để tiếp tục bán hàng.");

            var acceptedLines = await ValidateAcceptedLinesAsync(dto.Items, storeId, offline: false);
            if (!acceptedLines.IsSuccess)
                return ServiceResult<object>.Failure(acceptedLines.Message, errorCode: acceptedLines.ErrorCode);

            var iceSnapshots = await ValidateIceSnapshotsAsync(dto.Items, acceptedLines.Data, storeId);
            if (!iceSnapshots.IsSuccess || iceSnapshots.Data == null)
                return ServiceResult<object>.Failure(iceSnapshots.Message, errorCode: iceSnapshots.ErrorCode);

            await _repository.BeginTransactionAsync();
            var transactionCommitted = false;
            try
            {
                // Re-read inside the serializable transaction. UI state is never the authority.
                activeShift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure(
                        "Phiên POS không còn hoạt động.",
                        errorCode: WorkShiftErrorCodes.WorkShiftNotOpen);
                }

                var nowUtc = DateTime.UtcNow;
                if (activeShift.Status != WorkShiftStatuses.Open && activeShift.Status != "Open")
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure(
                        "Phiên POS đang chờ chốt két và không nhận giao dịch mới.",
                        errorCode: WorkShiftErrorCodes.WorkShiftPendingClose);
                }

                if (activeShift.AutoCloseAtUtc.HasValue && activeShift.AutoCloseAtUtc.Value <= nowUtc)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure(
                        "Phiên POS ngoài lịch đã hết hạn.",
                        errorCode: WorkShiftErrorCodes.WorkShiftExpired);
                }

                // 1. Calculate order totals
                decimal subTotal = 0;
                var orderDetails = new List<OrderDetail>();

                for (var itemIndex = 0; itemIndex < dto.Items.Count; itemIndex++)
                {
                    var item = dto.Items[itemIndex];
                    if (acceptedLines.Data != null)
                    {
                        var accepted = acceptedLines.Data[itemIndex];
                        subTotal += accepted.AcceptedUnitPrice * item.Quantity;
                        orderDetails.Add(BuildAcceptedOrderDetail(item, accepted, iceSnapshots.Data[itemIndex]));
                        continue;
                    }

                    var drink = await _repository.GetDrinkWithSizesAsync(item.DrinkId, storeId);
                    if (drink == null)
                        return ServiceResult<object>.Failure($"Sản phẩm #{item.DrinkId} không tồn tại hoặc không bán tại cửa hàng này.");

                    decimal itemBasePrice = 0;
                    string? sizeName = null;

                    if (item.SizeId.HasValue)
                    {
                        var drinkSize = drink.DrinkSizes.FirstOrDefault(ds => ds.SizeId == item.SizeId.Value && ds.Active);
                        if (drinkSize == null)
                            return ServiceResult<object>.Failure($"Size #{item.SizeId.Value} không hợp lệ cho sản phẩm {drink.Name}.");

                        itemBasePrice = drinkSize.Price;
                        sizeName = drinkSize.Size?.Name;
                    }
                    else
                    {
                        var defaultSize = drink.DrinkSizes
                            .Where(ds => ds.Active)
                            .OrderBy(ds => ds.Price)
                            .ThenBy(ds => ds.SizeId)
                            .FirstOrDefault();

                        if (defaultSize == null)
                            return ServiceResult<object>.Failure($"Sản phẩm {drink.Name} chưa có size đang hoạt động.");

                        itemBasePrice = defaultSize.Price;
                        sizeName = defaultSize.Size?.Name;
                        item.SizeId = defaultSize.SizeId;
                    }

                    decimal toppingTotal = 0;
                    var orderToppings = new List<OrderTopping>();
                    if (item.Toppings != null && item.Toppings.Any())
                    {
                        var toppingIds = item.Toppings.Select(t => t.ToppingId).Distinct().ToList();
                        var toppings = await _repository.GetValidToppingsForOrderItemAsync(storeId, item.DrinkId, toppingIds);
                        if (toppings.Count != toppingIds.Count)
                            return ServiceResult<object>.Failure($"Có topping không hợp lệ cho sản phẩm {drink.Name} hoặc cửa hàng hiện tại.");

                        foreach (var topping in toppings)
                        {
                            toppingTotal += topping.Price;
                            orderToppings.Add(new OrderTopping { ToppingId = topping.ToppingId, ToppingName = topping.Name, Price = topping.Price });
                        }
                    }

                    subTotal += (itemBasePrice + toppingTotal) * item.Quantity;
                    var orderDetail = new OrderDetail
                    {
                        DrinkId = item.DrinkId, SizeId = item.SizeId, DrinkName = drink.Name, SizeName = sizeName,
                        Price = itemBasePrice + toppingTotal, Quantity = item.Quantity, Note = item.Note ?? "", OrderToppings = orderToppings
                    };
                    ApplyIceSnapshot(orderDetail, iceSnapshots.Data[itemIndex]);
                    orderDetails.Add(orderDetail);
                }

                // Soft-removal: voucher + loyalty out of product scope — reject non-empty payload (no silent ignore).
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode) || dto.PointsUsed > 0)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure(
                        ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage,
                        errorCode: ProductScopeErrorCodes.FeatureNotAvailable);
                }

                // Selling total = server-priced lines only (DrinkSize + toppings). No voucher/points.
                const decimal voucherDiscount = 0m;
                const decimal pointDiscount = 0m;
                const int actualPointsUsed = 0;
                const int earnedPoints = 0;
                var total = subTotal;

                // 5. Create Order via Repository
                // ADR-0002: ClientOrderId được gán nguyên tử cùng Order — không tách bước
                var paymentLines = dto.Payments != null && dto.Payments.Any()
                    ? dto.Payments
                    : new List<PaymentLineDto> { new PaymentLineDto { PaymentMethodId = dto.PaymentMethodId > 0 ? dto.PaymentMethodId : 1, Amount = total } };

                if (paymentLines.Any(p => p.Amount < 0))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Số tiền thanh toán không được âm.");
                }

                if (paymentLines.Any(p => p.PaymentMethodId <= 0))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Phương thức thanh toán không hợp lệ.");
                }

                var paymentTotal = paymentLines.Sum(p => p.Amount);
                if (paymentTotal != total)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Tổng các dòng thanh toán phải bằng tổng tiền đơn hàng.");
                }

                var hasPayOsPayment = paymentLines.Any(p => p.PaymentMethodId == 2);
                var pendingCashAmount = paymentLines
                    .Where(p => p.PaymentMethodId == 1)
                    .Sum(p => p.Amount);
                var pendingVietQrAmount = paymentLines
                    .Where(p => p.PaymentMethodId == 2)
                    .Sum(p => p.Amount);
                var effectiveReceivedAmount = ResolveCashReceivedAmount(dto.ReceivedAmount, total);
                var cashChangeAmount = CalculateCashChangeAmount(effectiveReceivedAmount, total);

                foreach (var cashLine in paymentLines.Where(p => p.PaymentMethodId == 1))
                {
                    var cashLineError = POSCashAmountValidator.Validate(cashLine.Amount, _cashDenominationStep);
                    if (cashLineError != null)
                    {
                        await _repository.RollbackTransactionAsync();
                        return ServiceResult<object>.Failure(cashLineError, errorCode: "INVALID_CASH_DENOMINATION");
                    }
                }

                if (pendingCashAmount > 0)
                {
                    var receivedCashError = POSCashAmountValidator.Validate(
                        hasPayOsPayment ? pendingCashAmount : effectiveReceivedAmount,
                        _cashDenominationStep);
                    if (receivedCashError != null)
                    {
                        await _repository.RollbackTransactionAsync();
                        return ServiceResult<object>.Failure(receivedCashError, errorCode: "INVALID_CASH_DENOMINATION");
                    }
                }

                if (hasPayOsPayment && pendingVietQrAmount <= 0)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Số tiền VietQR còn lại không hợp lệ.");
                }

                if (hasPayOsPayment && paymentLines.Any(p => p.PaymentMethodId != 1 && p.PaymentMethodId != 2))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Tách thanh toán hiện chỉ hỗ trợ tiền mặt và VietQR.");
                }

                if (!hasPayOsPayment && pendingCashAmount > 0 && effectiveReceivedAmount < total)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Tiền khách đưa phải lớn hơn hoặc bằng tổng tiền đơn hàng.");
                }

                var orderStatusId = hasPayOsPayment
                    ? SystemConstants.OrderStatuses.AwaitingPayment
                    : SystemConstants.OrderStatuses.Completed;
                var paymentStatusId = hasPayOsPayment
                    ? SystemConstants.PaymentStatuses.Unpaid
                    : SystemConstants.PaymentStatuses.Paid;
                var operatorStaffId = activeShift.CurrentOperatorStaffId ?? activeShift.UserId;

                var newOrder = await _repository.CreateOrderAsync(new Order
                {
                    StoreId = storeId, StaffId = operatorStaffId, WorkShiftId = activeShift.ShiftId,
                    TerminalId = activeShift.PosTerminalId,
                    CustomerId = dto.CustomerId, OrderTypeId = dto.OrderTypeId > 0 ? dto.OrderTypeId : 1,
                    OrderStatusId = orderStatusId, PaymentStatusId = paymentStatusId, SubTotal = subTotal,
                    VoucherDiscount = voucherDiscount, PointDiscount = pointDiscount, PointsUsed = actualPointsUsed,
                    Total = total, ShippingFee = 0, Source = "POS", Note = dto.Note,
                    ClientOrderId = dto.ClientOrderId,  // ADR-0002: Idempotency Key — null cho đơn online
                    RecommendationSessionId = dto.RecommendationSessionId,
                    CreatedAt = DateTime.Now, OrderDetails = orderDetails
                });

                // 6. Create Payment records — Hỗ trợ thanh toán hỗn hợp (Split Payments)
                foreach (var payLine in paymentLines)
                {
                    await _repository.CreatePaymentAsync(new Payment
                    {
                        OrderId = newOrder.OrderId, PaymentMethodId = payLine.PaymentMethodId,
                        Amount = payLine.Amount,
                        ReceivedAmount = !hasPayOsPayment && payLine.PaymentMethodId == 1 ? effectiveReceivedAmount : null,
                        ChangeAmount = !hasPayOsPayment && payLine.PaymentMethodId == 1 ? cashChangeAmount : null,
                        PaymentStatusId = paymentStatusId,
                        StoreId = storeId,
                        WorkShiftId = activeShift.ShiftId,
                        PaidByStaffId = operatorStaffId,
                        TerminalId = activeShift.PosTerminalId,
                        PaidAt = hasPayOsPayment ? null : DateTime.Now
                    });
                }

                // Customer stats only (no earn/redeem points, no voucher usage — soft-removal).
                if (dto.CustomerId.HasValue)
                {
                    var customer = await _repository.GetCustomerByIdAsync(dto.CustomerId.Value);
                    if (customer != null)
                    {
                        customer.TotalSpent += total;
                        customer.TotalOrders += 1;
                        customer.LastOrderDate = DateTime.Now;
                        await _repository.UpdateCustomerAsync(customer);
                    }
                }

                // Update WorkShift.ExpectedEndingCash — cộng dồn tiền mặt trong cùng transaction
                // Chỉ cộng khi PaymentMethodId == 1 (Tiền mặt) → tiền vào két
                decimal cashAmount = paymentLines
                    .Where(p => p.PaymentMethodId == 1)
                    .Sum(p => p.Amount);

                if (!hasPayOsPayment && cashAmount > 0)
                {
                    activeShift.ExpectedEndingCash += cashAmount;
                    // EF Change Tracker sẽ detect thay đổi trên entity đã tracked
                    // SaveChangesAsync persist vào DB trong cùng transaction
                    await _repository.SaveChangesAsync();
                }

                await _repository.CommitTransactionAsync();
                transactionCommitted = true;

                if (hasPayOsPayment)
                {
                    PayOSCreateLinkResult paymentLink;
                    try
                    {
                        paymentLink = pendingVietQrAmount == total
                            ? await _payOSService.CreatePaymentLinkAsync(newOrder.OrderId)
                            : await _payOSService.CreatePaymentLinkAsync(newOrder.OrderId, pendingVietQrAmount);
                    }
                    catch (Exception payOsEx)
                    {
                        _logger.LogError(
                            payOsEx,
                            "[CommitOrder] Order #{OrderId} đã tạo nhưng không tạo được PayOS link.",
                            newOrder.OrderId);
                        return ServiceResult<object>.Failure("Đơn đã được tạo nhưng không thể tạo mã thanh toán VietQR: " + payOsEx.Message);
                    }

                    return ServiceResult<object>.Success(new
                    {
                        orderId = newOrder.OrderId,
                        subTotal,
                        voucherDiscount,
                        pointDiscount,
                        total,
                        receivedAmount = dto.ReceivedAmount,
                        changeAmount = 0m,
                        earnedPoints,
                        requiresPayment = true,
                        paymentMethodId = 2,
                        pendingCashAmount,
                        pendingVietQrAmount,
                        checkoutUrl = paymentLink.CheckoutUrl,
                        qrCode = paymentLink.QrCode,
                        orderCode = paymentLink.OrderCode
                    } as object, "Đã tạo mã thanh toán VietQR. Đang chờ khách quét mã.");
                }

                // ADR-0003: Trigger Silent Print sau commit thành công
                // Fire-and-forget — print failure KHÔNG ảnh hưởng order đã commit
                if (!dto.SkipPrint)
                {
                    try
                    {
                        // Xác định phương thức thanh toán có tiền mặt hay không (kick cash drawer)
                        bool hasCashPayment = (dto.Payments != null && dto.Payments.Any(p => p.PaymentMethodId == 1))
                            || dto.PaymentMethodId == 1;

                        // Lấy tên thu ngân từ Staff (nếu có) — fallback "POS"
                        var cashierName = newOrder.Staff?.FullName ?? "POS";

                        await _printDispatcher.DispatchPrintJobAsync(
                            newOrder, storeId, cashierName, effectiveReceivedAmount, hasCashPayment);
                    }
                    catch (Exception printEx)
                    {
                        // Double safety net — PrintDispatcher đã catch bên trong,
                        // nhưng phòng hờ edge case
                        _logger.LogError(printEx,
                            "[POSOrderService] Print dispatch failed cho Order #{OrderId}. Order đã commit thành công.",
                            newOrder.OrderId);
                    }
                }

                return ServiceResult<object>.Success(new
                {
                    orderId = newOrder.OrderId, subTotal, voucherDiscount, pointDiscount,
                    total, receivedAmount = effectiveReceivedAmount,
                    changeAmount = cashChangeAmount, earnedPoints
                } as object, "Thanh toán thành công!");
            }
            catch (Exception ex)
            {
                if (!transactionCommitted)
                {
                    await _repository.RollbackTransactionAsync();
                }

                return ServiceResult<object>.Failure("Lỗi hệ thống khi thanh toán: " + ex.Message);
            }
        }

        // ============================================================
        // COMMIT OFFLINE SYNCED ORDER — Issue #81
        // ============================================================
        public async Task<ServiceResult<object>> CommitOfflineSyncedOrderAsync(
            POSOrderCommitDto dto,
            OfflineOrderSyncContext syncContext)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return ServiceResult<object>.Failure("Giỏ hàng offline trống.");

            if (!dto.ClientOrderId.HasValue)
                return ServiceResult<object>.Failure("Thiếu ClientOrderId cho đơn offline.");

            if (syncContext == null || syncContext.ActorStaffId <= 0 || syncContext.WorkShiftId <= 0)
            {
                return ServiceResult<object>.Failure(
                    "Không xác định được người đồng bộ hoặc WorkShift gốc.",
                    errorCode: OrderAccessErrorCodes.Forbidden);
            }

            if ((dto.Payments != null && dto.Payments.Any(p => p.PaymentMethodId != 1)) ||
                ((dto.Payments == null || !dto.Payments.Any()) && dto.PaymentMethodId != 1))
            {
                return ServiceResult<object>.Failure("Offline Sync chỉ hỗ trợ thanh toán tiền mặt.");
            }

            var originalShift = await _workShiftService.GetShiftByIdAsync(syncContext.WorkShiftId);
            if (originalShift == null)
            {
                return ServiceResult<object>.Failure(
                    "Không tìm thấy WorkShift gốc cho đơn offline.",
                    errorCode: OrderAccessErrorCodes.WorkShiftNotFound);
            }

            if (originalShift.OpenContext == WorkShiftOpenContexts.OutsideSchedule)
            {
                return ServiceResult<object>.Failure(
                    "Phiên POS ngoài lịch không cho phép tạo đơn offline.",
                    errorCode: WorkShiftErrorCodes.OutsideScheduleOfflineNotAllowed);
            }

            var actor = new AdminActorContext
            {
                StaffId = syncContext.ActorStaffId,
                StoreId = syncContext.ClaimedStoreId,
                RoleNames = syncContext.ActorRoleNames
            };

            if (_orderAccessAuthorization == null)
            {
                return ServiceResult<object>.Failure(
                    "Không thể xác minh quyền đồng bộ đơn offline.",
                    errorCode: OrderAccessErrorCodes.Forbidden);
            }

            var access = await _orderAccessAuthorization.AuthorizeAsync(
                actor,
                OrderAccessActions.OfflineSync,
                originalShift.StoreId);
            if (access == OrderAccessDecision.Forbidden)
            {
                return ServiceResult<object>.Failure(
                    "Bạn không có quyền đồng bộ đơn POS.",
                    errorCode: OrderAccessErrorCodes.Forbidden);
            }

            if (access == OrderAccessDecision.NotFound)
            {
                return ServiceResult<object>.Failure(
                    "Không tìm thấy WorkShift gốc cho đơn offline.",
                    errorCode: OrderAccessErrorCodes.WorkShiftNotFound);
            }

            if (syncContext.ClaimedStaffId != originalShift.UserId ||
                syncContext.ClaimedStoreId != originalShift.StoreId)
            {
                _logger.LogWarning(
                    "[OfflineSync] Payload attribution mismatch | ActorStaffId={ActorStaffId} | " +
                    "WorkShiftId={WorkShiftId} | ClaimedStaffId={ClaimedStaffId} | " +
                    "ClaimedStoreId={ClaimedStoreId}",
                    syncContext.ActorStaffId,
                    originalShift.ShiftId,
                    syncContext.ClaimedStaffId,
                    syncContext.ClaimedStoreId);

                return ServiceResult<object>.Failure(
                    "Thông tin nhân viên/cửa hàng trong đơn offline không khớp WorkShift gốc.",
                    errorCode: OrderAccessErrorCodes.OfflineAttributionMismatch);
            }

            var userId = originalShift.UserId;
            var storeId = originalShift.StoreId;
            var soldAt = syncContext.SoldAt;

            _logger.LogInformation(
                "[OfflineSync] Authorized actor | ActorStaffId={ActorStaffId} | " +
                "AttributedStaffId={AttributedStaffId} | StoreId={StoreId} | WorkShiftId={WorkShiftId}",
                syncContext.ActorStaffId,
                userId,
                storeId,
                originalShift.ShiftId);

            var existingOrder = await _repository.FindOrderByClientOrderIdAsync(
                dto.ClientOrderId.Value,
                storeId);
            if (existingOrder != null)
            {
                _logger.LogInformation(
                    "[OfflineSync] Idempotent — Order #{OrderId} đã tồn tại cho ClientOrderId={ClientOrderId}",
                    existingOrder.OrderId, dto.ClientOrderId);

                return ServiceResult<object>.Success(BuildIdempotentResponse(existingOrder, dto.ReceivedAmount),
                    "Đơn offline đã được đồng bộ trước đó.");
            }

            // Soft-removal: offline must not carry voucher/loyalty effects.
            if (!string.IsNullOrWhiteSpace(dto.VoucherCode) || dto.PointsUsed > 0)
            {
                return ServiceResult<object>.Failure(
                    ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage,
                    errorCode: ProductScopeErrorCodes.FeatureNotAvailable);
            }

            var acceptedLines = await ValidateAcceptedLinesAsync(dto.Items, storeId, offline: true);
            if (!acceptedLines.IsSuccess)
                return ServiceResult<object>.Failure(acceptedLines.Message, errorCode: acceptedLines.ErrorCode);

            var iceSnapshots = await ValidateIceSnapshotsAsync(dto.Items, acceptedLines.Data, storeId);
            if (!iceSnapshots.IsSuccess || iceSnapshots.Data == null)
                return ServiceResult<object>.Failure(iceSnapshots.Message, errorCode: iceSnapshots.ErrorCode);

            await _repository.BeginTransactionAsync();
            var transactionCommitted = false;

            try
            {
                decimal subTotal = 0;
                var orderDetails = new List<OrderDetail>();

                for (var itemIndex = 0; itemIndex < dto.Items.Count; itemIndex++)
                {
                    var item = dto.Items[itemIndex];
                    if (item.Quantity <= 0)
                    {
                        await _repository.RollbackTransactionAsync();
                        return ServiceResult<object>.Failure("Số lượng món trong đơn offline phải lớn hơn 0.");
                    }

                    if (acceptedLines.Data != null)
                    {
                        var accepted = acceptedLines.Data[itemIndex];
                        subTotal += accepted.AcceptedUnitPrice * item.Quantity;
                        orderDetails.Add(BuildAcceptedOrderDetail(item, accepted, iceSnapshots.Data[itemIndex]));
                        continue;
                    }

                    var drink = await _repository.GetDrinkWithSizesAsync(item.DrinkId, storeId);
                    if (drink == null)
                    {
                        await _repository.RollbackTransactionAsync();
                        return ServiceResult<object>.Failure($"Sản phẩm #{item.DrinkId} không tồn tại hoặc không bán tại cửa hàng này.");
                    }

                    decimal itemBasePrice;
                    string? sizeName;

                    if (item.SizeId.HasValue)
                    {
                        var drinkSize = drink.DrinkSizes.FirstOrDefault(ds => ds.SizeId == item.SizeId.Value && ds.Active);
                        if (drinkSize == null)
                        {
                            await _repository.RollbackTransactionAsync();
                            return ServiceResult<object>.Failure($"Size #{item.SizeId.Value} không hợp lệ cho sản phẩm {drink.Name}.");
                        }

                        itemBasePrice = drinkSize.Price;
                        sizeName = drinkSize.Size?.Name;
                    }
                    else
                    {
                        var defaultSize = drink.DrinkSizes
                            .Where(ds => ds.Active)
                            .OrderBy(ds => ds.Price)
                            .ThenBy(ds => ds.SizeId)
                            .FirstOrDefault();

                        if (defaultSize == null)
                        {
                            await _repository.RollbackTransactionAsync();
                            return ServiceResult<object>.Failure($"Sản phẩm {drink.Name} chưa có size đang hoạt động.");
                        }

                        itemBasePrice = defaultSize.Price;
                        sizeName = defaultSize.Size?.Name;
                        item.SizeId = defaultSize.SizeId;
                    }

                    decimal toppingTotal = 0;
                    var orderToppings = new List<OrderTopping>();
                    if (item.Toppings != null && item.Toppings.Any())
                    {
                        var toppingIds = item.Toppings.Select(t => t.ToppingId).Distinct().ToList();
                        var toppings = await _repository.GetValidToppingsForOrderItemAsync(storeId, item.DrinkId, toppingIds);
                        if (toppings.Count != toppingIds.Count)
                        {
                            await _repository.RollbackTransactionAsync();
                            return ServiceResult<object>.Failure($"Có topping không hợp lệ cho sản phẩm {drink.Name} hoặc cửa hàng hiện tại.");
                        }

                        foreach (var topping in toppings)
                        {
                            toppingTotal += topping.Price;
                            orderToppings.Add(new OrderTopping
                            {
                                ToppingId = topping.ToppingId,
                                ToppingName = topping.Name,
                                Price = topping.Price
                            });
                        }
                    }

                    subTotal += (itemBasePrice + toppingTotal) * item.Quantity;
                    var orderDetail = new OrderDetail
                    {
                        DrinkId = item.DrinkId,
                        SizeId = item.SizeId,
                        DrinkName = drink.Name,
                        SizeName = sizeName,
                        Price = itemBasePrice + toppingTotal,
                        Quantity = item.Quantity,
                        Note = item.Note ?? "",
                        OrderToppings = orderToppings
                    };
                    ApplyIceSnapshot(orderDetail, iceSnapshots.Data[itemIndex]);
                    orderDetails.Add(orderDetail);
                }

                var total = subTotal;
                var paymentLines = dto.Payments != null && dto.Payments.Any()
                    ? dto.Payments
                    : new List<PaymentLineDto> { new PaymentLineDto { PaymentMethodId = 1, Amount = total } };

                if (paymentLines.Any(p => p.PaymentMethodId != 1))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Offline Sync chỉ hỗ trợ thanh toán tiền mặt.");
                }

                if (paymentLines.Any(p => p.Amount < 0))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Số tiền thanh toán không được âm.");
                }

                var paymentTotal = paymentLines.Sum(p => p.Amount);
                if (paymentTotal != total)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Tổng thanh toán offline không khớp tổng tiền đơn hàng.");
                }

                var effectiveReceivedAmount = ResolveCashReceivedAmount(dto.ReceivedAmount, total);
                var cashChangeAmount = CalculateCashChangeAmount(effectiveReceivedAmount, total);
                var cashLineError = paymentLines
                    .Select(p => POSCashAmountValidator.Validate(p.Amount, _cashDenominationStep))
                    .FirstOrDefault(error => error != null);
                var receivedCashError = POSCashAmountValidator.Validate(effectiveReceivedAmount, _cashDenominationStep);
                if (cashLineError != null || receivedCashError != null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure(
                        cashLineError ?? receivedCashError!,
                        errorCode: "INVALID_CASH_DENOMINATION");
                }
                if (effectiveReceivedAmount < total)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<object>.Failure("Tiền khách đưa phải lớn hơn hoặc bằng tổng tiền đơn hàng.");
                }

                var createdAt = soldAt == default ? DateTime.Now : soldAt;
                var newOrder = await _repository.CreateOrderAsync(new Order
                {
                    StoreId = storeId,
                    StaffId = userId,
                    WorkShiftId = originalShift.ShiftId,
                    TerminalId = originalShift.PosTerminalId,
                    CustomerId = dto.CustomerId,
                    OrderTypeId = dto.OrderTypeId > 0 ? dto.OrderTypeId : 1,
                    OrderStatusId = SystemConstants.OrderStatuses.Completed,
                    PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                    SubTotal = subTotal,
                    VoucherDiscount = 0,
                    PointDiscount = 0,
                    PointsUsed = 0,
                    Total = total,
                    ShippingFee = 0,
                    Source = "POS",
                    Note = dto.Note,
                    ClientOrderId = dto.ClientOrderId,
                    RecommendationSessionId = dto.RecommendationSessionId,
                    CreatedAt = createdAt,
                    OrderDetails = orderDetails
                });

                foreach (var payLine in paymentLines)
                {
                    await _repository.CreatePaymentAsync(new Payment
                    {
                        OrderId = newOrder.OrderId,
                        PaymentMethodId = 1,
                        Amount = payLine.Amount,
                        ReceivedAmount = effectiveReceivedAmount,
                        ChangeAmount = cashChangeAmount,
                        PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                        StoreId = storeId,
                        WorkShiftId = originalShift.ShiftId,
                        PaidByStaffId = userId,
                        TerminalId = originalShift.PosTerminalId,
                        PaidAt = createdAt
                    });
                }

                var cashAmount = paymentLines.Sum(p => p.Amount);
                if (string.Equals(originalShift.Status, "Open", StringComparison.OrdinalIgnoreCase) && cashAmount > 0)
                {
                    originalShift.ExpectedEndingCash += cashAmount;
                    await _repository.SaveChangesAsync();
                }
                else if (!string.Equals(originalShift.Status, "Open", StringComparison.OrdinalIgnoreCase))
                {
                    originalShift.RequiresReconciliation = true;
                    originalShift.HasLateOfflineSync = true;
                    originalShift.LateOfflineSyncCount += 1;
                    originalShift.LastLateOfflineSyncedAtUtc = DateTime.UtcNow;
                    await _repository.SaveChangesAsync();
                }

                await _repository.CommitTransactionAsync();
                transactionCommitted = true;

                return ServiceResult<object>.Success(new
                {
                    orderId = newOrder.OrderId,
                    clientOrderId = newOrder.ClientOrderId?.ToString(),
                    workShiftId = newOrder.WorkShiftId,
                    storeId = newOrder.StoreId,
                    subTotal,
                    total,
                    receivedAmount = effectiveReceivedAmount,
                    changeAmount = cashChangeAmount,
                    isIdempotent = false
                } as object, "Đồng bộ đơn offline thành công.");
            }
            catch (Exception ex)
            {
                if (!transactionCommitted)
                {
                    await _repository.RollbackTransactionAsync();
                }

                var racedOrder = await _repository.FindOrderByClientOrderIdAsync(
                    dto.ClientOrderId.Value,
                    storeId);
                if (racedOrder != null)
                {
                    _logger.LogWarning(
                        ex,
                        "[OfflineSync] Unique race handled as duplicate for ClientOrderId={ClientOrderId}",
                        dto.ClientOrderId);

                    return ServiceResult<object>.Success(BuildIdempotentResponse(racedOrder, dto.ReceivedAmount),
                        "Đơn offline đã được đồng bộ trước đó.");
                }

                return ServiceResult<object>.Failure("Lỗi hệ thống khi đồng bộ đơn offline: " + ex.Message);
            }
        }

        private static object BuildIdempotentResponse(Order existingOrder, decimal receivedAmount)
        {
            var effectiveReceivedAmount = ResolveCashReceivedAmount(receivedAmount, existingOrder.Total);
            var change = CalculateCashChangeAmount(effectiveReceivedAmount, existingOrder.Total);
            return new
            {
                orderId = existingOrder.OrderId,
                clientOrderId = existingOrder.ClientOrderId?.ToString(),
                workShiftId = existingOrder.WorkShiftId,
                storeId = existingOrder.StoreId,
                subTotal = existingOrder.SubTotal,
                total = existingOrder.Total,
                receivedAmount = effectiveReceivedAmount,
                changeAmount = change,
                earnedPoints = 0,
                isIdempotent = true
            } as object;
        }

        private async Task<ServiceResult<IReadOnlyList<POSAcceptedSaleLineDto>?>> ValidateAcceptedLinesAsync(
            IReadOnlyList<POSOrderItemDto> items,
            int storeId,
            bool offline)
        {
            // Compatibility path for existing non-Store-Menu callers and focused legacy tests.
            // Production DI always supplies the validator and therefore requires full snapshots.
            if (_storeMenuSaleValidator == null)
                return ServiceResult<IReadOnlyList<POSAcceptedSaleLineDto>?>.Success(null);

            var accepted = new List<POSAcceptedSaleLineDto>(items.Count);
            var validationAtUtc = DateTime.UtcNow;
            foreach (var item in items)
            {
                var result = offline
                    ? await _storeMenuSaleValidator.ValidateOfflineAsync(item, storeId)
                    : await _storeMenuSaleValidator.ValidateOnlineAsync(item, storeId, validationAtUtc);
                if (!result.IsSuccess || result.Data == null)
                {
                    return ServiceResult<IReadOnlyList<POSAcceptedSaleLineDto>?>.Failure(
                        result.Message,
                        errorCode: result.ErrorCode);
                }

                accepted.Add(result.Data);
            }

            return ServiceResult<IReadOnlyList<POSAcceptedSaleLineDto>?>.Success(accepted);
        }

        private static OrderDetail BuildAcceptedOrderDetail(
            POSOrderItemDto item,
            POSAcceptedSaleLineDto accepted,
            POSIceOrderSnapshotDto? iceSnapshot)
        {
            var detail = new OrderDetail
            {
                DrinkId = accepted.DrinkId,
                SizeId = accepted.SizeId,
                StoreMenuItemId = accepted.StoreMenuItemId,
                DrinkSizeId = accepted.DrinkSizeId,
                DrinkName = accepted.DrinkName,
                SizeName = accepted.SizeName,
                Price = accepted.AcceptedUnitPrice,
                AcceptedBasePrice = accepted.AcceptedBasePrice,
                PriceSource = accepted.PriceSource,
                AcceptedCatalogVersion = accepted.CatalogVersion,
                Quantity = item.Quantity,
                Note = item.Note ?? string.Empty,
                OrderToppings = accepted.Toppings.Select(x => new OrderTopping
                {
                    ToppingId = x.ToppingId,
                    ToppingName = x.Name,
                    Price = x.AcceptedPrice
                }).ToList()
            };
            ApplyIceSnapshot(detail, iceSnapshot);
            return detail;
        }

        private async Task<ServiceResult<IReadOnlyList<POSIceOrderSnapshotDto?>>> ValidateIceSnapshotsAsync(
            IReadOnlyList<POSOrderItemDto> items,
            IReadOnlyList<POSAcceptedSaleLineDto>? acceptedLines,
            int storeId)
        {
            var snapshots = new List<POSIceOrderSnapshotDto?>(items.Count);
            if (_iceCustomization == null)
            {
                snapshots.AddRange(Enumerable.Repeat<POSIceOrderSnapshotDto?>(null, items.Count));
                return ServiceResult<IReadOnlyList<POSIceOrderSnapshotDto?>>.Success(snapshots);
            }

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var sizeId = acceptedLines != null ? acceptedLines[index].SizeId : item.SizeId;
                var result = await _iceCustomization.CreateOrderSnapshotAsync(
                    storeId,
                    item.DrinkId,
                    sizeId,
                    item.Quantity,
                    item.IceLevelPercent);
                if (!result.IsSuccess)
                {
                    return ServiceResult<IReadOnlyList<POSIceOrderSnapshotDto?>>.Failure(
                        $"{result.Message} (món #{item.DrinkId}).",
                        errorCode: result.ErrorCode);
                }

                snapshots.Add(result.Data);
            }

            return ServiceResult<IReadOnlyList<POSIceOrderSnapshotDto?>>.Success(snapshots);
        }

        private static void ApplyIceSnapshot(OrderDetail detail, POSIceOrderSnapshotDto? snapshot)
        {
            if (snapshot == null)
                return;

            detail.IceLevelPercent = snapshot.IceLevelPercent;
            detail.IceIngredientId = snapshot.IceIngredientId;
            detail.BaseIceQuantityBaseUnit = snapshot.BaseIceQuantityBaseUnit;
            detail.AppliedIceQuantityBaseUnit = snapshot.AppliedIceQuantityBaseUnit;
        }

        private static decimal ResolveCashReceivedAmount(decimal receivedAmount, decimal total)
        {
            return receivedAmount <= 0m ? total : receivedAmount;
        }

        private static decimal CalculateCashChangeAmount(decimal receivedAmount, decimal total)
        {
            var change = receivedAmount - total;
            return change > 0m ? change : 0m;
        }

        private static bool IsCompatibleIdempotentReplay(Order existingOrder, POSOrderCommitDto dto)
        {
            var requestedOrderTypeId = dto.OrderTypeId > 0 ? dto.OrderTypeId : 1;
            if ((existingOrder.OrderTypeId > 0 && existingOrder.OrderTypeId != requestedOrderTypeId) ||
                existingOrder.CustomerId != dto.CustomerId)
            {
                return false;
            }

            var requestedPayments = dto.Payments != null && dto.Payments.Any()
                ? dto.Payments
                    .GroupBy(payment => payment.PaymentMethodId)
                    .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount))
                : new Dictionary<int, decimal>
                {
                    [dto.PaymentMethodId > 0 ? dto.PaymentMethodId : 1] = existingOrder.Total
                };
            var existingPayments = existingOrder.Payments
                .GroupBy(payment => payment.PaymentMethodId)
                .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

            if (existingPayments.Values.Any(amount => amount > 0m) &&
                (requestedPayments.Count != existingPayments.Count ||
                requestedPayments.Any(pair => !existingPayments.TryGetValue(pair.Key, out var amount) || amount != pair.Value))
               )
            {
                return false;
            }

            var existingCashPayment = existingOrder.Payments
                .FirstOrDefault(payment => payment.PaymentMethodId == 1 && payment.ReceivedAmount.HasValue);
            if (existingCashPayment?.ReceivedAmount is decimal existingReceivedAmount &&
                ResolveCashReceivedAmount(dto.ReceivedAmount, existingOrder.Total) != existingReceivedAmount)
            {
                return false;
            }

            if (existingOrder.OrderDetails == null || existingOrder.OrderDetails.Count == 0)
                return true;

            if (existingOrder.OrderDetails.Count != dto.Items.Count)
                return false;

            var requestedItems = dto.Items
                .Select(item => new
                {
                    item.DrinkId,
                    item.SizeId,
                    item.Quantity,
                    item.IceLevelPercent,
                    Toppings = string.Join(",", item.Toppings.Select(topping => topping.ToppingId).OrderBy(id => id))
                })
                .OrderBy(item => item.DrinkId)
                .ThenBy(item => item.SizeId)
                .ThenBy(item => item.Quantity)
                .ThenBy(item => item.Toppings)
                .ToList();
            var existingItems = existingOrder.OrderDetails
                .Select(item => new
                {
                    item.DrinkId,
                    item.SizeId,
                    item.Quantity,
                    item.IceLevelPercent,
                    Toppings = string.Join(",", item.OrderToppings.Select(topping => topping.ToppingId).OrderBy(id => id))
                })
                .OrderBy(item => item.DrinkId)
                .ThenBy(item => item.SizeId)
                .ThenBy(item => item.Quantity)
                .ThenBy(item => item.Toppings)
                .ToList();

            return requestedItems.SequenceEqual(existingItems);
        }

        // ============================================================
        // GET CLOSE SHIFT DATA
        // ============================================================
        public async Task<ServiceResult<object>> GetCloseShiftDataAsync(int userId, int storeId)
        {
            var activeShift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (activeShift == null)
                return ServiceResult<object>.Failure("Không tìm thấy ca két tiền đang mở.");

            var totalCashSales = await _repository.GetTotalSalesByPaymentMethodAsync(activeShift.ShiftId, 1);
            var totalQrSales = await _repository.GetTotalSalesByPaymentMethodAsync(activeShift.ShiftId, 2);
            var totalOrders = await _repository.GetCompletedOrderCountAsync(activeShift.ShiftId);

            var expectedEndingCash = activeShift.StartingCash + totalCashSales;
            var duration = DateTime.UtcNow - activeShift.StartTimeUtc;

            return ServiceResult<object>.Success(new
            {
                activeShift.ShiftId,
                startTime = activeShift.StartTimeUtc.ToString("O"),
                currentTime = DateTime.Now.ToString("hh:mm tt"),
                durationHours = (int)duration.TotalHours,
                durationMinutes = duration.Minutes,
                totalOrders, startingCash = activeShift.StartingCash,
                totalCashSales, totalQrSales, cashChangeGiven = 0m,
                expectedEndingCash, netRevenue = totalCashSales + totalQrSales
            } as object);
        }

        // ============================================================
        // GET ORDER HISTORY — Issue #68: Phân trang lịch sử đơn hàng
        // ============================================================
        public async Task<ServiceResult<object>> GetOrderHistoryAsync(int storeId, int page, int pageSize)
        {
            // Guard: page/pageSize hợp lệ
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;  // Hard cap tránh abuse

            var (items, totalCount) = await _repository.GetOrderHistoryAsync(storeId, page, pageSize);

            return ServiceResult<object>.Success(new
            {
                items,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            } as object);
        }

        // ============================================================
        // REPRINT ORDER — Issue #83: Intentional receipt/label reprint
        // ============================================================
        public async Task<ServiceResult<object>> ReprintOrderAsync(
            int orderId,
            POSOrderReprintRequestDto dto,
            int storeId)
        {
            var type = dto?.Type?.Trim();
            var normalizedType = type?.ToLowerInvariant();

            if (normalizedType != "receipt" && normalizedType != "drinklabel")
                return ServiceResult<object>.Failure("Loại in lại không hợp lệ. Chỉ hỗ trợ receipt hoặc drinkLabel.");

            var order = await _repository.GetOrderForReprintAsync(orderId, storeId);
            if (order == null)
            {
                return ServiceResult<object>.Failure(
                    "Không tìm thấy đơn hàng để in lại.",
                    errorCode: OrderAccessErrorCodes.NotFound);
            }

            if (!string.Equals(order.Source, "POS", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<object>.Failure("Chỉ hỗ trợ in lại đơn POS đã đồng bộ.");

            if (order.OrderStatusId == SystemConstants.OrderStatuses.Cancelled)
                return ServiceResult<object>.Failure("Đơn đã hủy không thể in lại.");

            if (order.PaymentStatusId != SystemConstants.PaymentStatuses.Paid)
                return ServiceResult<object>.Failure("Chỉ có thể in lại đơn đã thanh toán.");

            var cashierName = order.Staff?.FullName ?? "POS";
            var cashReceived = order.Payments?
                .Where(payment => payment.PaymentMethodId == 1 && payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                .Sum(payment => payment.Amount) ?? 0m;
            var hasCashPayment = cashReceived > 0;
            var receiptCashReceived = hasCashPayment ? cashReceived : order.Total;

            bool dispatched;
            string message;
            if (normalizedType == "receipt")
            {
                dispatched = await _printDispatcher.DispatchReceiptReprintAsync(
                    order,
                    order.StoreId,
                    cashierName,
                    receiptCashReceived,
                    hasCashPayment);
                message = "Đã gửi lệnh in lại hóa đơn.";
            }
            else
            {
                dispatched = await _printDispatcher.DispatchDrinkLabelReprintAsync(
                    order,
                    order.StoreId,
                    cashierName);
                message = "Đã gửi lệnh in lại tem.";
            }

            if (!dispatched)
                return ServiceResult<object>.Failure("Không gửi được lệnh in lại. Vui lòng kiểm tra PrintBridge.");

            return ServiceResult<object>.Success(new
            {
                orderId = order.OrderId,
                type = normalizedType
            } as object, message);
        }
    }
}

