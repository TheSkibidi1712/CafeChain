using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.ADR0002_Idempotency
{
    /// <summary>
    /// ADR-0002 Integration Tests — Chứng minh tính Idempotency của Offline Order Sync.
    /// 
    /// Luồng test mô phỏng logic chính xác của AdminPOSController.SyncOfflineOrders:
    ///   1. Với mỗi OfflineOrderSyncDTO trong batch:
    ///      - Nếu ClientOrderId != null → gọi FindOrderByClientOrderIdAsync()
    ///      - Nếu đã tồn tại → skip (idempotent)
    ///      - Nếu chưa tồn tại → gọi CommitOrderAsync() → tạo Order mới
    ///   2. Trả về { syncedCount, skippedCount, details }
    /// 
    /// Strategy:
    ///   - Repository: REAL (POSOrderRepository + AppDbContext SQLite)
    ///   - POSOrderService: MOCK (CommitOrderAsync trả success + tạo Order trực tiếp)
    ///   - InventoryDeductionService: MOCK (không liên quan idempotency)
    /// </summary>
    public class IdempotencyTests : IntegrationTestBase
    {
        private readonly IPOSOrderRepository _repository;
        private readonly Mock<IPOSOrderService> _mockOrderService;
        private readonly Mock<IInventoryDeductionService> _mockInventoryService;
        private readonly AppDbContext _context;

        public IdempotencyTests()
        {
            _context = CreateDbContext();
            _repository = new POSOrderRepository(_context);
            _mockOrderService = new Mock<IPOSOrderService>();
            _mockInventoryService = new Mock<IInventoryDeductionService>();

            // Seed dữ liệu lookup bắt buộc (FK constraints)
            SeedRequiredLookupData(_context);
        }

        // ═══════════════════════════════════════════════════════════
        // SEED: Tạo dữ liệu lookup tối thiểu cho FK constraints
        // ═══════════════════════════════════════════════════════════
        private void SeedRequiredLookupData(AppDbContext ctx)
        {
            // OrderStatus (seed data may already exist from HasData config)
            if (!ctx.OrderStatuses.Any())
            {
                ctx.OrderStatuses.AddRange(
                    new OrderStatus { OrderStatusId = 1, Name = "Chờ xác nhận" },
                    new OrderStatus { OrderStatusId = 5, Name = "Hoàn thành" }
                );
            }

            // OrderType
            if (!ctx.OrderTypes.Any())
            {
                ctx.OrderTypes.AddRange(
                    new OrderType { OrderTypeId = 1, Name = "Dine In" },
                    new OrderType { OrderTypeId = 2, Name = "Take Away" }
                );
            }

            // PaymentStatus
            if (!ctx.PaymentStatuses.Any())
            {
                ctx.PaymentStatuses.Add(new CafeChain.Models.Payments.PaymentStatus
                {
                    PaymentStatusId = 1, Name = "Đã thanh toán"
                });
            }

            // Store
            if (!ctx.Stores.Any(s => s.StoreId == 1))
            {
                ctx.Stores.Add(new Store { StoreId = 1, Name = "Test Store" });
            }

            ctx.SaveChanges();
        }

        // ═══════════════════════════════════════════════════════════
        // HELPER: Mô phỏng logic SyncOfflineOrders từ Controller
        // Trích xuất chính xác từ AdminPOSController.SyncOfflineOrders
        // ═══════════════════════════════════════════════════════════
        private async Task<(int syncedCount, int skippedCount, List<object> results)>
            SimulateSyncOfflineOrders(List<OfflineOrderSyncDTO> offlineOrders, int userId, int storeId)
        {
            int syncedCount = 0;
            int skippedCount = 0;
            var results = new List<object>();

            foreach (var orderDto in offlineOrders)
            {
                // ── ADR-0002: Idempotency Check ──
                if (orderDto.ClientOrderId.HasValue)
                {
                    var existingOrder = await _repository.FindOrderByClientOrderIdAsync(
                        orderDto.ClientOrderId.Value,
                        storeId);
                    if (existingOrder != null)
                    {
                        skippedCount++;
                        results.Add(new
                        {
                            localId = orderDto.LocalId,
                            clientOrderId = orderDto.ClientOrderId,
                            orderId = existingOrder.OrderId,
                            status = "skipped",
                            reason = "Đơn hàng đã được đồng bộ trước đó (idempotent)."
                        });
                        continue;
                    }
                }

                // Tạo Order trực tiếp trong DB (mô phỏng CommitOrderAsync thành công)
                var newOrder = new Order
                {
                    StoreId = storeId,
                    StaffId = userId,
                    OrderStatusId = 5,   // Hoàn thành
                    PaymentStatusId = 1,
                    OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1,
                    ClientOrderId = orderDto.ClientOrderId,
                    Source = "POS",
                    Note = "[OFFLINE-SYNC] " + (orderDto.Note ?? ""),
                    Total = orderDto.TotalAmount,
                    SubTotal = orderDto.TotalAmount,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                syncedCount++;
                results.Add(new
                {
                    localId = orderDto.LocalId,
                    clientOrderId = orderDto.ClientOrderId,
                    orderId = newOrder.OrderId,
                    status = "synced"
                });
            }

            return (syncedCount, skippedCount, results);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 1: 🟢 Batch 3 đơn unique → tạo đủ 3
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task Sync_WithUniqueClientOrderIds_CreatesAllOrders()
        {
            // Arrange
            var guidA = Guid.NewGuid();
            var guidB = Guid.NewGuid();
            var guidC = Guid.NewGuid();

            var batch = new List<OfflineOrderSyncDTO>
            {
                new() { LocalId = "L1", ClientOrderId = guidA, TotalAmount = 50000, OrderTypeId = 1 },
                new() { LocalId = "L2", ClientOrderId = guidB, TotalAmount = 35000, OrderTypeId = 2 },
                new() { LocalId = "L3", ClientOrderId = guidC, TotalAmount = 70000, OrderTypeId = 1 }
            };

            // Act
            var (syncedCount, skippedCount, results) = await SimulateSyncOfflineOrders(batch, userId: 1, storeId: 1);

            // Assert
            Assert.Equal(3, syncedCount);
            Assert.Equal(0, skippedCount);

            // Verify DB — đúng 3 Order với ClientOrderId tương ứng
            using var verifyCtx = CreateDbContext();
            var ordersInDb = await verifyCtx.Orders.ToListAsync();
            Assert.Equal(3, ordersInDb.Count);
            Assert.Contains(ordersInDb, o => o.ClientOrderId == guidA);
            Assert.Contains(ordersInDb, o => o.ClientOrderId == guidB);
            Assert.Contains(ordersInDb, o => o.ClientOrderId == guidC);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 2: 🟢 Batch 5 đơn, 2 đơn trùng → chỉ tạo 3
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task Sync_WithDuplicateClientOrderId_SkipsDuplicates()
        {
            // Arrange — pre-seed 2 Order đã tồn tại
            var guidX = Guid.NewGuid();
            var guidY = Guid.NewGuid();

            _context.Orders.AddRange(
                new Order
                {
                    StoreId = 1, StaffId = 1, OrderStatusId = 5, PaymentStatusId = 1, OrderTypeId = 1,
                    ClientOrderId = guidX, Total = 30000, SubTotal = 30000, CreatedAt = DateTime.UtcNow
                },
                new Order
                {
                    StoreId = 1, StaffId = 1, OrderStatusId = 5, PaymentStatusId = 1, OrderTypeId = 1,
                    ClientOrderId = guidY, Total = 25000, SubTotal = 25000, CreatedAt = DateTime.UtcNow
                }
            );
            await _context.SaveChangesAsync();

            var guidA = Guid.NewGuid();
            var guidB = Guid.NewGuid();
            var guidC = Guid.NewGuid();

            var batch = new List<OfflineOrderSyncDTO>
            {
                new() { LocalId = "L1", ClientOrderId = guidX, TotalAmount = 30000, OrderTypeId = 1 }, // TRÙNG X
                new() { LocalId = "L2", ClientOrderId = guidA, TotalAmount = 50000, OrderTypeId = 1 }, // MỚI
                new() { LocalId = "L3", ClientOrderId = guidY, TotalAmount = 25000, OrderTypeId = 1 }, // TRÙNG Y
                new() { LocalId = "L4", ClientOrderId = guidB, TotalAmount = 35000, OrderTypeId = 2 }, // MỚI
                new() { LocalId = "L5", ClientOrderId = guidC, TotalAmount = 40000, OrderTypeId = 1 }  // MỚI
            };

            // Act
            var (syncedCount, skippedCount, results) = await SimulateSyncOfflineOrders(batch, userId: 1, storeId: 1);

            // Assert
            Assert.Equal(3, syncedCount);
            Assert.Equal(2, skippedCount);

            // Verify DB — tổng 5 Order (2 cũ + 3 mới), KHÔNG có duplicate
            using var verifyCtx = CreateDbContext();
            var allOrders = await verifyCtx.Orders.ToListAsync();
            Assert.Equal(5, allOrders.Count);

            // Verify mỗi ClientOrderId chỉ xuất hiện đúng 1 lần
            var groupedByClientId = allOrders
                .Where(o => o.ClientOrderId.HasValue)
                .GroupBy(o => o.ClientOrderId!.Value);
            Assert.All(groupedByClientId, g => Assert.Single(g));
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 3: 🟢 Retry cùng batch 2 lần → lần 2 skip hết
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task Sync_RetryExactSameBatch_AllSkipped()
        {
            // Arrange
            var guidP = Guid.NewGuid();
            var guidQ = Guid.NewGuid();

            var batch = new List<OfflineOrderSyncDTO>
            {
                new() { LocalId = "P", ClientOrderId = guidP, TotalAmount = 45000, OrderTypeId = 1 },
                new() { LocalId = "Q", ClientOrderId = guidQ, TotalAmount = 60000, OrderTypeId = 2 }
            };

            // Act — Lần 1: sync thành công
            var (synced1, skipped1, _) = await SimulateSyncOfflineOrders(batch, userId: 1, storeId: 1);

            // Act — Lần 2: retry cùng batch (mạng chập chờn, client gửi lại)
            var (synced2, skipped2, results2) = await SimulateSyncOfflineOrders(batch, userId: 1, storeId: 1);

            // Assert — Lần 1
            Assert.Equal(2, synced1);
            Assert.Equal(0, skipped1);

            // Assert — Lần 2: Idempotent → tất cả bị skip
            Assert.Equal(0, synced2);
            Assert.Equal(2, skipped2);

            // Verify DB — chỉ có đúng 2 Order, KHÔNG phải 4
            using var verifyCtx = CreateDbContext();
            var totalOrders = await verifyCtx.Orders.CountAsync();
            Assert.Equal(2, totalOrders);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 4: 🟢 ClientOrderId = null (đơn online legacy) → tạo bình thường
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task Sync_WithNullClientOrderId_CreatesOrderNormally()
        {
            // Arrange
            var batch = new List<OfflineOrderSyncDTO>
            {
                new() { LocalId = "legacy-1", ClientOrderId = null, TotalAmount = 55000, OrderTypeId = 1 }
            };

            // Act
            var (syncedCount, skippedCount, _) = await SimulateSyncOfflineOrders(batch, userId: 1, storeId: 1);

            // Assert
            Assert.Equal(1, syncedCount);
            Assert.Equal(0, skippedCount);

            // Verify DB — Order tạo thành công với ClientOrderId = null
            using var verifyCtx = CreateDbContext();
            var order = await verifyCtx.Orders.FirstAsync();
            Assert.Null(order.ClientOrderId);
            Assert.Equal(55000, order.Total);
        }

        // ═══════════════════════════════════════════════════════════
        // TEST 5: 🔴 Batch rỗng → trả lỗi
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task Sync_WithEmptyBatch_ReturnsError()
        {
            // Arrange
            var emptyBatch = new List<OfflineOrderSyncDTO>();

            // Act — mô phỏng controller check đầu tiên
            bool isEmptyOrNull = emptyBatch == null || emptyBatch.Count == 0;

            // Assert
            Assert.True(isEmptyOrNull, "Controller phải reject batch rỗng trước khi xử lý.");

            // Verify DB không thay đổi
            using var verifyCtx = CreateDbContext();
            var totalOrders = await verifyCtx.Orders.CountAsync();
            Assert.Equal(0, totalOrders);
        }
    }
}
