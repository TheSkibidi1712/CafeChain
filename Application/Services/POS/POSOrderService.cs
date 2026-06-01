using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Loyalties;
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

        public POSOrderService(IPOSOrderRepository repository, IWorkShiftService workShiftService, IAdminVoucherService voucherService)
        {
            _repository = repository;
            _workShiftService = workShiftService;
            _voucherService = voucherService;
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
        // COMMIT ORDER — Core business logic
        // ============================================================
        public async Task<ServiceResult<object>> CommitOrderAsync(POSOrderCommitDto dto, int userId, int storeId)
        {
            var activeShift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (activeShift == null)
                return ServiceResult<object>.Failure("Phiên két tiền đã đóng, vui lòng mở ca mới để tiếp tục bán hàng.");

            await _repository.BeginTransactionAsync();
            try
            {
                // 1. Calculate order totals
                decimal subTotal = 0;
                var orderDetails = new List<OrderDetail>();

                foreach (var item in dto.Items)
                {
                    var drink = await _repository.GetDrinkWithSizesAsync(item.DrinkId);
                    if (drink == null) continue;

                    decimal itemBasePrice = 0;
                    string? sizeName = null;

                    if (item.SizeId.HasValue)
                    {
                        var drinkSize = drink.DrinkSizes.FirstOrDefault(ds => ds.SizeId == item.SizeId.Value);
                        if (drinkSize != null) { itemBasePrice = drinkSize.Price; sizeName = drinkSize.Size?.Name; }
                    }
                    else
                    {
                        var defaultSize = drink.DrinkSizes.FirstOrDefault(ds => ds.Active);
                        if (defaultSize != null) { itemBasePrice = defaultSize.Price; sizeName = defaultSize.Size?.Name; item.SizeId = defaultSize.SizeId; }
                    }

                    decimal toppingTotal = 0;
                    var orderToppings = new List<OrderTopping>();
                    if (item.Toppings != null && item.Toppings.Any())
                    {
                        var toppingIds = item.Toppings.Select(t => t.ToppingId).ToList();
                        var toppings = await _repository.GetToppingsByIdsAsync(toppingIds);
                        foreach (var topping in toppings)
                        {
                            toppingTotal += topping.Price;
                            orderToppings.Add(new OrderTopping { ToppingId = topping.ToppingId, ToppingName = topping.Name, Price = topping.Price });
                        }
                    }

                    subTotal += (itemBasePrice + toppingTotal) * item.Quantity;
                    orderDetails.Add(new OrderDetail
                    {
                        DrinkId = item.DrinkId, SizeId = item.SizeId, DrinkName = drink.Name, SizeName = sizeName,
                        Price = itemBasePrice + toppingTotal, Quantity = item.Quantity, Note = item.Note ?? "", OrderToppings = orderToppings
                    });
                }

                // 2. Apply Voucher
                decimal voucherDiscount = 0;
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var voucherResult = await _voucherService.ValidateVoucherAsync(dto.VoucherCode, dto.CustomerId ?? 0, subTotal);
                    if (voucherResult.Success && voucherResult.Voucher != null)
                    {
                        var voucher = voucherResult.Voucher;
                        if (voucher.DiscountAmount.HasValue) voucherDiscount = voucher.DiscountAmount.Value;
                        else if (voucher.DiscountPercent.HasValue)
                        {
                            voucherDiscount = (subTotal * voucher.DiscountPercent.Value) / 100;
                            if (voucher.MaxDiscount.HasValue && voucherDiscount > voucher.MaxDiscount.Value)
                                voucherDiscount = voucher.MaxDiscount.Value;
                        }
                    }
                }

                // 3. Apply Loyalty Points — Safety Guard: max 50% SubTotal
                decimal pointDiscount = 0;
                int actualPointsUsed = 0;
                if (dto.CustomerId.HasValue && dto.PointsUsed > 0)
                {
                    var customer = await _repository.GetCustomerByIdAsync(dto.CustomerId.Value);
                    if (customer != null && customer.CurrentPoints >= dto.PointsUsed)
                    {
                        actualPointsUsed = dto.PointsUsed;
                        pointDiscount = actualPointsUsed * 1000m;
                        decimal maxPointDiscount = subTotal * 0.50m;
                        if (pointDiscount > maxPointDiscount)
                        {
                            pointDiscount = maxPointDiscount;
                            actualPointsUsed = (int)(maxPointDiscount / 1000m);
                        }
                    }
                }

                // 4. Calculate Final Total
                var total = Math.Max(0, subTotal - voucherDiscount - pointDiscount);

                // 5. Create Order via Repository
                var newOrder = await _repository.CreateOrderAsync(new Order
                {
                    StoreId = storeId, StaffId = userId > 0 ? userId : null, WorkShiftId = activeShift.ShiftId,
                    CustomerId = dto.CustomerId, OrderTypeId = dto.OrderTypeId > 0 ? dto.OrderTypeId : 1,
                    OrderStatusId = 4, PaymentStatusId = 2, SubTotal = subTotal,
                    VoucherDiscount = voucherDiscount, PointDiscount = pointDiscount, PointsUsed = actualPointsUsed,
                    Total = total, ShippingFee = 0, Source = "POS", Note = dto.Note,
                    CreatedAt = DateTime.Now, OrderDetails = orderDetails
                });

                // 6. Create Payment records via Repository
                var paymentLines = dto.Payments != null && dto.Payments.Any()
                    ? dto.Payments
                    : new List<PaymentLineDto> { new PaymentLineDto { PaymentMethodId = dto.PaymentMethodId > 0 ? dto.PaymentMethodId : 1, Amount = total } };

                foreach (var payLine in paymentLines)
                {
                    await _repository.CreatePaymentAsync(new Payment
                    {
                        OrderId = newOrder.OrderId, PaymentMethodId = payLine.PaymentMethodId,
                        Amount = payLine.Amount, PaymentStatusId = 2, PaidAt = DateTime.Now
                    });
                }

                // 7. Handle Loyalty Points via Repository
                int earnedPoints = 0;
                if (dto.CustomerId.HasValue)
                {
                    var customer = await _repository.GetCustomerByIdAsync(dto.CustomerId.Value);
                    if (customer != null)
                    {
                        if (actualPointsUsed > 0)
                        {
                            customer.CurrentPoints -= actualPointsUsed;
                            await _repository.CreatePointTransactionAsync(new PointTransaction
                            {
                                CustomerId = customer.CustomerId, OrderId = newOrder.OrderId,
                                Points = actualPointsUsed, PointTransactionTypeId = 2,
                                BalanceAfter = customer.CurrentPoints, CreatedAt = DateTime.Now
                            });
                        }
                        earnedPoints = (int)(total / 10000);
                        if (earnedPoints > 0)
                        {
                            customer.CurrentPoints += earnedPoints;
                            await _repository.CreatePointTransactionAsync(new PointTransaction
                            {
                                CustomerId = customer.CustomerId, OrderId = newOrder.OrderId,
                                Points = earnedPoints, PointTransactionTypeId = 1,
                                BalanceAfter = customer.CurrentPoints, CreatedAt = DateTime.Now
                            });
                        }
                        customer.TotalSpent += total;
                        customer.TotalOrders += 1;
                        customer.LastOrderDate = DateTime.Now;
                        await _repository.UpdateCustomerAsync(customer);
                    }
                }

                // 8. Handle Voucher Usage via Repository
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode) && voucherDiscount > 0)
                {
                    var voucher = await _repository.GetVoucherByCodeAsync(dto.VoucherCode);
                    if (voucher != null)
                    {
                        await _repository.CreateOrderVoucherAsync(new Models.Vouchers.OrderVoucher
                        { OrderId = newOrder.OrderId, VoucherId = voucher.VoucherId, DiscountValue = voucherDiscount });
                        if (dto.CustomerId.HasValue)
                        {
                            await _repository.CreateVoucherUsageAsync(new Models.Vouchers.VoucherUsage
                            { VoucherId = voucher.VoucherId, CustomerId = dto.CustomerId.Value, UsedAt = DateTime.Now });
                        }
                    }
                }

                await _repository.CommitTransactionAsync();

                var changeAmount = dto.ReceivedAmount - total;
                return ServiceResult<object>.Success(new
                {
                    orderId = newOrder.OrderId, subTotal, voucherDiscount, pointDiscount,
                    total, receivedAmount = dto.ReceivedAmount,
                    changeAmount = changeAmount > 0 ? changeAmount : 0, earnedPoints
                } as object, "Thanh toán thành công!");
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                return ServiceResult<object>.Failure("Lỗi hệ thống khi thanh toán: " + ex.Message);
            }
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
            var duration = DateTime.Now - activeShift.StartTime;

            return ServiceResult<object>.Success(new
            {
                activeShift.ShiftId,
                startTime = activeShift.StartTime.ToString("hh:mm tt"),
                currentTime = DateTime.Now.ToString("hh:mm tt"),
                durationHours = (int)duration.TotalHours,
                durationMinutes = duration.Minutes,
                totalOrders, startingCash = activeShift.StartingCash,
                totalCashSales, totalQrSales, cashChangeGiven = 0m,
                expectedEndingCash, netRevenue = totalCashSales + totalQrSales
            } as object);
        }
    }
}
