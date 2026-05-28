using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using CafeChain.Models.Loyalties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminPOSController : Controller
    {
        private readonly IWorkShiftService _workShiftService;
        private readonly AppDbContext _context;

        public AdminPOSController(IWorkShiftService workShiftService, AppDbContext context)
        {
            _workShiftService = workShiftService;
            _context = context;
        }

        // ============================================================
        // VIEW: Main POS Screen
        // ============================================================
        public async Task<IActionResult> Index()
        {
            // 1. Get logged-in user accountId from Claims
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // 2. Check Role: must be Cashier ("Thu ngân") or Shift Supervisor ("Ca trưởng")
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isCashier = role == CafeChain.Application.Constants.RoleConstants.Cashier;
            var isSupervisor = role == CafeChain.Application.Constants.RoleConstants.ShiftSupervisor;
            var isStoreManager = role == CafeChain.Application.Constants.RoleConstants.StoreManager;
            var isSuperAdmin = role == CafeChain.Application.Constants.RoleConstants.SuperAdmin;

            // cashier or supervisor require an active shift
            if (isCashier || isSupervisor)
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy hồ sơ nhân viên.";
                    return RedirectToAction("Index", "StaffHub", new { area = "" });
                }

                // Check for active shift today or yesterday (overnight shift)
                var hasActiveShift = await _context.StaffShifts.AnyAsync(ss => 
                    ss.StaffId == staff.StaffId && 
                    (ss.WorkDate.Date == today || ss.WorkDate.Date == yesterday) && 
                    ss.ActualCheckIn.HasValue && 
                    !ss.ActualCheckOut.HasValue);

                if (!hasActiveShift)
                {
                    TempData["ErrorMessage"] = "Bạn phải Vào Ca chấm công trước khi truy cập hệ thống POS.";
                    return RedirectToAction("Index", "StaffHub", new { area = "" });
                }
            }
            else if (!isStoreManager && !isSuperAdmin) // Other roles cannot access POS
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn không có quyền truy cập POS.";
                return RedirectToAction("Index", "StaffHub", new { area = "" });
            }

            return View();
        }

        // ============================================================
        // API: Get Active WorkShift
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetActiveShift()
        {
            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            var shift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (shift == null)
            {
                return Json(new { success = true, hasActiveShift = false });
            }

            return Json(new
            {
                success = true,
                hasActiveShift = true,
                shift = new
                {
                    shift.ShiftId,
                    shift.StartTime,
                    shift.StartingCash,
                    shift.ExpectedEndingCash,
                    shift.Status
                }
            });
        }

        // ============================================================
        // API: Open Shift
        // ============================================================
        public class OpenShiftRequest
        {
            public decimal StartingCash { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request)
        {
            try
            {
                var (userId, storeId) = await ResolveUserStoreAsync();
                if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

                var result = await _workShiftService.OpenShiftAsync(userId, storeId, request.StartingCash);
                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ============================================================
        // API: Close Shift (Cash Reconciliation)
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequestDto request)
        {
            try
            {
                var (userId, storeId) = await ResolveUserStoreAsync();
                if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

                var result = await _workShiftService.CloseShiftAsync(userId, storeId, request);
                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ============================================================
        // API: Get Menu Data (Products by Store)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetMenuData()
        {
            var (_, storeId) = await ResolveUserStoreAsync();

            // Get all active drinks available at this store
            var storeDrinkIds = await _context.StoreDrinks
                .Where(sd => sd.StoreId == storeId && sd.Active)
                .Select(sd => sd.DrinkId)
                .ToListAsync();

            // Get categories with their drinks
            var categories = await _context.DrinkCategories
                .Where(c => c.Active)
                .Select(c => new
                {
                    c.CategoryId,
                    c.Name,
                    Drinks = c.Drinks
                        .Where(d => d.Active && storeDrinkIds.Contains(d.DrinkId))
                        .Select(d => new
                        {
                            d.DrinkId,
                            d.Name,
                            ImageUrl = d.DrinkImages
                                .Where(di => di.IsDefault)
                                .Select(di => di.ImageUrl)
                                .FirstOrDefault() ?? d.DrinkImages.Select(di => di.ImageUrl).FirstOrDefault(),
                            Sizes = d.DrinkSizes
                                .Where(ds => ds.Active)
                                .Select(ds => new
                                {
                                    ds.SizeId,
                                    SizeName = ds.Size.Name,
                                    ds.Price
                                }).ToList(),
                            Toppings = d.DrinkToppings
                                .Select(dt => new
                                {
                                    dt.ToppingId,
                                    ToppingName = dt.Topping.Name,
                                    Price = dt.Topping.Price,
                                    ImageUrl = dt.Topping.ImageUrl
                                }).ToList()
                        }).ToList()
                })
                .Where(c => c.Drinks.Any())
                .ToListAsync();

            // Also get store-level toppings (available for all drinks)
            var storeToppings = await _context.StoreToppings
                .Where(st => st.StoreId == storeId && st.Active)
                .Select(st => new
                {
                    st.ToppingId,
                    ToppingName = st.Topping.Name,
                    Price = st.Topping.Price,
                    ImageUrl = st.Topping.ImageUrl
                }).ToListAsync();

            return Json(new { success = true, categories, storeToppings });
        }

        // ============================================================
        // API: Commit POS Order (Transactional)
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CommitOrder([FromBody] POSOrderCommitDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return Json(new { success = false, message = "Giỏ hàng trống." });

            var (userId, storeId) = await ResolveUserStoreAsync();
            if (userId == 0) return Json(new { success = false, message = "Không xác định được tài khoản." });

            // Verify active WorkShift
            var activeShift = await _workShiftService.GetActiveShiftAsync(userId, storeId);
            if (activeShift == null)
                return Json(new { success = false, message = "Phiên két tiền đã đóng, vui lòng mở ca mới để tiếp tục bán hàng." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Calculate order totals
                decimal subTotal = 0;
                var orderDetails = new List<OrderDetail>();

                foreach (var item in dto.Items)
                {
                    // Get drink price based on size
                    decimal itemBasePrice = 0;
                    string drinkName = "";
                    string? sizeName = null;

                    var drink = await _context.Drinks
                        .Include(d => d.DrinkSizes).ThenInclude(ds => ds.Size)
                        .FirstOrDefaultAsync(d => d.DrinkId == item.DrinkId);

                    if (drink == null) continue;
                    drinkName = drink.Name;

                    if (item.SizeId.HasValue)
                    {
                        var drinkSize = drink.DrinkSizes.FirstOrDefault(ds => ds.SizeId == item.SizeId.Value);
                        if (drinkSize != null)
                        {
                            itemBasePrice = drinkSize.Price;
                            sizeName = drinkSize.Size?.Name;
                        }
                    }
                    else
                    {
                        // Default: first size price or 0
                        var defaultSize = drink.DrinkSizes.FirstOrDefault(ds => ds.Active);
                        if (defaultSize != null)
                        {
                            itemBasePrice = defaultSize.Price;
                            sizeName = defaultSize.Size?.Name;
                            item.SizeId = defaultSize.SizeId;
                        }
                    }

                    // Calculate topping total
                    decimal toppingTotal = 0;
                    var orderToppings = new List<OrderTopping>();

                    if (item.Toppings != null && item.Toppings.Any())
                    {
                        var toppingIds = item.Toppings.Select(t => t.ToppingId).ToList();
                        var toppings = await _context.Toppings
                            .Where(t => toppingIds.Contains(t.ToppingId))
                            .ToListAsync();

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

                    var lineTotal = (itemBasePrice + toppingTotal) * item.Quantity;
                    subTotal += lineTotal;

                    var orderDetail = new OrderDetail
                    {
                        DrinkId = item.DrinkId,
                        SizeId = item.SizeId,
                        DrinkName = drinkName,
                        SizeName = sizeName,
                        Price = itemBasePrice + toppingTotal,
                        Quantity = item.Quantity,
                        Note = item.Note ?? "",
                        OrderToppings = orderToppings
                    };
                    orderDetails.Add(orderDetail);
                }

                // 2. Apply Voucher Discount
                decimal voucherDiscount = 0;
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.Code == dto.VoucherCode && v.Active && v.EndDate >= DateTime.Now);

                    if (voucher != null)
                    {
                        if (voucher.DiscountAmount.HasValue)
                        {
                            voucherDiscount = voucher.DiscountAmount.Value;
                        }
                        else if (voucher.DiscountPercent.HasValue)
                        {
                            voucherDiscount = (subTotal * voucher.DiscountPercent.Value) / 100;
                            if (voucher.MaxDiscount.HasValue && voucherDiscount > voucher.MaxDiscount.Value)
                                voucherDiscount = voucher.MaxDiscount.Value;
                        }
                    }
                }

                // 3. Apply Loyalty Points Discount
                decimal pointDiscount = 0;
                int actualPointsUsed = 0;
                if (dto.CustomerId.HasValue && dto.PointsUsed > 0)
                {
                    var customer = await _context.Customers.FindAsync(dto.CustomerId.Value);
                    if (customer != null && customer.CurrentPoints >= dto.PointsUsed)
                    {
                        actualPointsUsed = dto.PointsUsed;
                        pointDiscount = actualPointsUsed * 1000m; // 1 point = 1,000 VND
                    }
                }

                // 4. Calculate Final Total
                var total = subTotal - voucherDiscount - pointDiscount;
                if (total < 0) total = 0;

                // 5. Create Order Record
                var staffId = await GetStaffIdFromAccountAsync();
                var newOrder = new Order
                {
                    StoreId = storeId,
                    StaffId = staffId,
                    WorkShiftId = activeShift.ShiftId,
                    CustomerId = dto.CustomerId,
                    OrderTypeId = dto.OrderTypeId > 0 ? dto.OrderTypeId : 1,
                    OrderStatusId = 4, // Completed / Paid
                    PaymentStatusId = 2, // Paid
                    SubTotal = subTotal,
                    VoucherDiscount = voucherDiscount,
                    PointDiscount = pointDiscount,
                    PointsUsed = actualPointsUsed,
                    Total = total,
                    ShippingFee = 0,
                    Source = "POS",
                    Note = dto.Note,
                    CreatedAt = DateTime.Now,
                    OrderDetails = orderDetails
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                // 6. Handle Loyalty Points
                if (dto.CustomerId.HasValue)
                {
                    var customer = await _context.Customers.FindAsync(dto.CustomerId.Value);
                    if (customer != null)
                    {
                        // Deduct used points
                        if (actualPointsUsed > 0)
                        {
                            customer.CurrentPoints -= actualPointsUsed;
                            _context.PointTransactions.Add(new PointTransaction
                            {
                                CustomerId = customer.CustomerId,
                                OrderId = newOrder.OrderId,
                                Points = actualPointsUsed,
                                PointTransactionTypeId = 2, // Deduction
                                BalanceAfter = customer.CurrentPoints,
                                CreatedAt = DateTime.Now
                            });
                        }

                        // Earn points (1 point per 10,000 VND spent)
                        int earnedPoints = (int)(total / 10000);
                        if (earnedPoints > 0)
                        {
                            customer.CurrentPoints += earnedPoints;
                            _context.PointTransactions.Add(new PointTransaction
                            {
                                CustomerId = customer.CustomerId,
                                OrderId = newOrder.OrderId,
                                Points = earnedPoints,
                                PointTransactionTypeId = 1, // Earn
                                BalanceAfter = customer.CurrentPoints,
                                CreatedAt = DateTime.Now
                            });
                        }

                        // Update customer stats
                        customer.TotalSpent += total;
                        customer.TotalOrders += 1;
                        customer.LastOrderDate = DateTime.Now;

                        await _context.SaveChangesAsync();
                    }
                }

                // 7. Handle Voucher Usage
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode) && voucherDiscount > 0)
                {
                    var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == dto.VoucherCode);
                    if (voucher != null)
                    {
                        _context.OrderVouchers.Add(new Models.Vouchers.OrderVoucher
                        {
                            OrderId = newOrder.OrderId,
                            VoucherId = voucher.VoucherId,
                            DiscountValue = voucherDiscount
                        });

                        if (dto.CustomerId.HasValue)
                        {
                            _context.VoucherUsages.Add(new Models.Vouchers.VoucherUsage
                            {
                                VoucherId = voucher.VoucherId,
                                CustomerId = dto.CustomerId.Value,
                                UsedAt = DateTime.Now
                            });
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                var changeAmount = dto.ReceivedAmount - total;
                return Json(new
                {
                    success = true,
                    message = "Thanh toán thành công!",
                    orderId = newOrder.OrderId,
                    subTotal,
                    voucherDiscount,
                    pointDiscount,
                    total,
                    receivedAmount = dto.ReceivedAmount,
                    changeAmount = changeAmount > 0 ? changeAmount : 0
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống khi thanh toán: " + ex.Message });
            }
        }

        // ============================================================
        // API: Search Customer by Phone
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> SearchCustomer(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Json(new { success = false, message = "Vui lòng nhập số điện thoại." });

            var customer = await _context.CustomerPhones
                .Where(cp => cp.Phone.Contains(phone))
                .Select(cp => new
                {
                    cp.Customer.CustomerId,
                    cp.Customer.FullName,
                    Phone = cp.Phone,
                    cp.Customer.CurrentPoints,
                    cp.Customer.TotalOrders,
                    cp.Customer.TotalSpent,
                    MemberLevel = cp.Customer.MemberLevel != null ? cp.Customer.MemberLevel.Name : "Thành viên"
                })
                .FirstOrDefaultAsync();

            if (customer == null)
                return Json(new { success = false, message = "Không tìm thấy khách hàng." });

            return Json(new { success = true, customer });
        }

        // ============================================================
        // API: Sync Offline Orders
        // ============================================================
        [HttpPost("Admin/AdminPOS/SyncOfflineOrders")]
        public async Task<IActionResult> SyncOfflineOrders([FromBody] List<OfflineOrderSyncDTO> offlineOrders)
        {
            if (offlineOrders == null || offlineOrders.Count == 0)
                return Json(new { success = false, message = "Không có dữ liệu đồng bộ." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int syncedCount = 0;
                
                foreach (var orderDto in offlineOrders)
                {
                    // 1. Create main order
                    var newOrder = new Order
                    {
                        Total = orderDto.TotalAmount,
                        OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1, // Default DineIn
                        OrderStatusId = 4, // Trạng thái: Đã thanh toán / Hoàn thành
                        CreatedAt = DateTime.UtcNow,
                        StoreId = orderDto.StoreId ?? 1,
                        Note = "[OFFLINE-SYNC] " + orderDto.Note
                    };

                    _context.Orders.Add(newOrder);
                    await _context.SaveChangesAsync(); // Get OrderId

                    // 2. Create order details
                    foreach (var item in orderDto.Details)
                    {
                        var orderDetail = new OrderDetail
                        {
                            OrderId = newOrder.OrderId,
                            DrinkId = item.ItemId,
                            Quantity = item.Quantity,
                            Price = item.UnitPrice
                        };
                        _context.OrderDetails.Add(orderDetail);
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // 3. BLACKBOX INVENTORY DEDUCTION 
                    // TODO: Trigger Inventory Deduction Event / Service here.
                    // DO NOT TOUCH OR MODIFY INVENTORY SYSTEM.
                    // _inventoryDeductionService.DeductForOrder(newOrder.OrderId);

                    syncedCount++;
                }

                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã đồng bộ thành công {syncedCount} đơn hàng ngoại tuyến." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống khi đồng bộ: " + ex.Message });
            }
        }

        // ============================================================
        // PRIVATE: Resolve UserId (StaffId) and StoreId from Claims
        // ============================================================
        private async Task<(int userId, int storeId)> ResolveUserStoreAsync()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return (0, 0);

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            if (staff == null) return (0, 0);

            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            int storeId = int.TryParse(storeIdClaim, out int sid) ? sid : staff.StoreId;

            return (staff.StaffId, storeId);
        }

        private async Task<int?> GetStaffIdFromAccountAsync()
        {
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
                return null;

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.AccountId == accountId);
            return staff?.StaffId;
        }
    }
}
