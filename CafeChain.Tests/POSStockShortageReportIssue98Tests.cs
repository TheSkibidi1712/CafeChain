using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Services.Inventories;
using CafeChain.Controllers.Api.v1;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Operations;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #98 — POS shortage report + StaffNotification + non-blocking email.</summary>
    public class POSStockShortageReportIssue98Tests : IntegrationTestBase
    {
        private const int StoreId = 80;
        private const int OtherStoreId = 81;
        private const int IngredientId = 8801;
        private const int RecipeId = 8901;
        private const int UnitId = 1;
        private const int SalesStaffId = 801;
        private const int SupervisorStaffId = 802;
        private const int ManagerStaffId = 803;
        private const int AccountantStaffId = 804;
        private const int CustomerStaffId = 805;

        [Fact]
        public async Task SalesStaff_ReportsIngredient_CreatesAlertAndNotifications()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 2m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "sm@test.local");
            SeedStaffWithRole(ctx, AccountantStaffId, 9004, RoleConstants.AccountantWarehouse, "aw@test.local");
            await ctx.SaveChangesAsync();

            var email = CreateEmailMock(throwOnSend: false);
            var service = CreateService(ctx, email.Object);
            var invId = await IngredientInventoryIdAsync(ctx);

            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = invId,
                    Note = "Hết sữa trên quầy, cần kiểm tra kho."
                });

            Assert.True(result.IsSuccess);
            Assert.Equal("created", result.Data!.CreatedOrUpdated);
            Assert.Equal(2, result.Data.NotificationCount);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
            Assert.Equal(StockAlertSources.SalesReport, alert.Source);
            Assert.Equal(IngredientId, alert.IngredientId);
            Assert.Equal(SalesStaffId, alert.ReportedByStaffId);
            Assert.Equal("Hết sữa trên quầy, cần kiểm tra kho.", alert.Note);
            Assert.Equal(StockAlertTypes.LowStock, alert.AlertType);

            Assert.Equal(2, await ctx.StaffNotifications.CountAsync());
            Assert.All(
                await ctx.StaffNotifications.ToListAsync(),
                n => Assert.Equal(StaffNotificationTypes.StockShortageReport, n.Type));
        }

        [Fact]
        public async Task ReportShortage_WithNotificationDelivery_UsesDecisionSeverity()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 2m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var audience = new Mock<IInventoryNotificationAudienceResolver>();
            audience
                .Setup(x => x.ResolveAsync(
                    StoreId,
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(Array.Empty<InventoryNotificationRecipient>());

            var delivery = new Mock<IInventoryNotificationDeliveryService>();
            delivery
                .Setup(x => x.DeliverAsync(
                    It.IsAny<InventoryNotificationDeliveryRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new InventoryNotificationDeliveryResult(
                    1,
                    0,
                    0,
                    true,
                    Array.Empty<StaffNotification>()));

            var service = new StockShortageReportService(
                ctx,
                CreateEmailMock(false).Object,
                new Mock<ILogger<StockShortageReportService>>().Object,
                delivery.Object,
                audience.Object);

            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                    Note = "Hết sữa trên quầy, cần kiểm tra kho."
                });

            Assert.True(result.IsSuccess, result.Message);
            delivery.Verify(
                x => x.DeliverAsync(
                    It.Is<InventoryNotificationDeliveryRequest>(
                        request => request.Severity == StockAlertSeverities.Warning),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ManualReport_UsesUsableQuantity_AndReusesConfirmedAlert()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 12m, min: 5m, ingredientReserved: 8m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var request = new StockShortageReportRequestDto
            {
                StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                Note = "Quầy chỉ còn lượng khả dụng thấp."
            };

            var first = await service.ReportShortageAsync(StoreId, SalesStaffId, request);
            Assert.True(first.IsSuccess);
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(4m, alert.CurrentQtySnapshot);

            alert.Status = StockAlertStatuses.Confirmed;
            await ctx.SaveChangesAsync();
            request.Note = "Báo lại sau khi kiểm tra lượng giữ chỗ.";

            var second = await service.ReportShortageAsync(StoreId, SalesStaffId, request);

            Assert.True(second.IsSuccess);
            Assert.Equal("updated", second.Data!.CreatedOrUpdated);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());
            Assert.Equal(StockAlertStatuses.Confirmed, (await ctx.StockAlerts.SingleAsync()).Status);
        }

        [Fact]
        public async Task ShiftSupervisor_ReportsRecipeBtp_CreatesAlert()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 10m, min: null, recipeQty: 0m);
            SeedStaffWithRole(ctx, SupervisorStaffId, 9002, RoleConstants.ShiftSupervisor, "ss@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "sm@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var recipeInvId = await RecipeInventoryIdAsync(ctx);

            var result = await service.ReportShortageAsync(
                StoreId,
                SupervisorStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = recipeInvId,
                    Note = "BTP syrup đã hết trên bar.",
                    Reason = "Chưa có ngưỡng nhưng quầy đã hết.",
                    TargetStockBaseQuantity = 5m
                });

            Assert.True(result.IsSuccess);
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(RecipeId, alert.RecipeId);
            Assert.Null(alert.IngredientId);
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Urgent, alert.Severity);
            Assert.Equal(StockAlertSources.SalesReport, alert.Source);
        }

        [Fact]
        public async Task StoreManager_CanReport()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 1m, min: 5m);
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "sm@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var invId = await IngredientInventoryIdAsync(ctx);

            var result = await service.ReportShortageAsync(
                StoreId,
                ManagerStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = invId,
                    Note = "Quản lý xác nhận thiếu hàng sau kiểm tra."
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(ManagerStaffId, (await ctx.StockAlerts.SingleAsync()).ReportedByStaffId);
        }

        [Fact]
        public async Task MinStockLevelNull_ManualReport_StillCreatesOpenAlert()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 5m, min: null);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var invId = await IngredientInventoryIdAsync(ctx);

            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = invId,
                    Note = "Báo thiếu dù chưa cấu hình ngưỡng.",
                    Reason = "Chuẩn bị sự kiện tại cửa hàng.",
                    TargetStockBaseQuantity = 8m
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));
            Assert.Null((await ctx.StoreInventories.FirstAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task ManualShortage_RequiresReasonAndTarget_WhenAvailableAtOrAboveThreshold()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 12m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "manager@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                    Note = "Kệ pha chế dự kiến thiếu cho sự kiện."
                });

            Assert.False(result.IsSuccess);
            Assert.Contains("Lý do", result.Message);
            Assert.Empty(await ctx.StockAlerts.ToListAsync());
        }

        [Fact]
        public async Task AboveThreshold_ManualTarget_CreatesReviewAlertWithAuditableTarget()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 12m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                    Note = "Đã đối chiếu lịch đặt bàn cuối tuần.",
                    Reason = "Nhu cầu sự kiện tăng cao.",
                    TargetStockBaseQuantity = 20m
                });

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(result.Data!.IsOutOfThresholdDemand);
            Assert.Equal(8m, result.Data.SuggestedBaseQuantity);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertTypes.ManualReview, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Review, alert.Severity);
            Assert.Equal(20m, alert.ThresholdSnapshot);

            var transition = await ctx.StockAlertTransitions.SingleAsync();
            Assert.Equal(10m, transition.MinLevelSnapshot);
            Assert.Equal("Nhu cầu sự kiện tăng cao.", transition.Reason);
            Assert.Equal(StockAlertSources.SalesReport, transition.SourceType);
        }

        [Fact]
        public async Task AboveThreshold_Forecast_NormalizesToDecisionTarget()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 12m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var result = await service.ReportShortageAsync(
                StoreId,
                SalesStaffId,
                new StockShortageReportRequestDto
                {
                    StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                    Note = "Dự báo dựa trên lượng đặt trước đã xác nhận.",
                    Reason = "Đơn đặt trước tăng nhu cầu.",
                    ForecastDemandUntilDeliveryBaseQuantity = 6m
                });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(18m, result.Data!.DecisionTargetBaseQuantity);
            Assert.Equal(6m, result.Data.SuggestedBaseQuantity);
            Assert.Equal(18m, (await ctx.StockAlerts.SingleAsync()).ThresholdSnapshot);
        }

        [Fact]
        public async Task NewManualDemand_ReopensPreviouslyConfirmedAlertForVerification()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 12m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "manager@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var request = new StockShortageReportRequestDto
            {
                StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                Note = "Đợt báo đầu tiên theo lịch sự kiện.",
                Reason = "Sự kiện làm tăng nhu cầu.",
                TargetStockBaseQuantity = 20m
            };
            Assert.True((await service.ReportShortageAsync(StoreId, SalesStaffId, request)).IsSuccess);

            var alert = await ctx.StockAlerts.SingleAsync();
            alert.Status = StockAlertStatuses.Confirmed;
            alert.ConfirmedByStaffId = ManagerStaffId;
            alert.ConfirmedAt = System.DateTime.UtcNow;
            alert.ManagerNote = "Đã duyệt mục tiêu cũ.";
            await ctx.SaveChangesAsync();

            request.Note = "Cập nhật bằng chứng cho nhu cầu mới.";
            request.TargetStockBaseQuantity = 22m;
            var second = await service.ReportShortageAsync(StoreId, SalesStaffId, request);

            Assert.True(second.IsSuccess, second.Message);
            alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
            Assert.Null(alert.ConfirmedByStaffId);
            Assert.Null(alert.ConfirmedAt);
            Assert.Null(alert.ManagerNote);
            Assert.Equal(22m, alert.ThresholdSnapshot);
        }

        [Fact]
        public async Task ReReport_UpdatesSameOpenAlert_NoDuplicate()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 3m, min: 10m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, SupervisorStaffId, 9002, RoleConstants.ShiftSupervisor, "ss@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var invId = await IngredientInventoryIdAsync(ctx);

            await service.ReportShortageAsync(StoreId, SalesStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = invId,
                Note = "Lần báo thứ nhất thiếu hàng."
            });

            var second = await service.ReportShortageAsync(StoreId, SupervisorStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = invId,
                Note = "Lần báo thứ hai — ghi chú mới."
            });

            Assert.True(second.IsSuccess);
            Assert.Equal("updated", second.Data!.CreatedOrUpdated);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal("Lần báo thứ hai — ghi chú mới.", alert.Note);
            Assert.Equal(SupervisorStaffId, alert.ReportedByStaffId);
        }

        [Fact]
        public async Task OtherStoreInventory_IsRejected()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 1m, min: 5m);
            // Other store row
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = OtherStoreId,
                IngredientId = IngredientId,
                AvailableQty = 1,
                ReservedQty = 0,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();

            var otherId = await ctx.StoreInventories
                .Where(i => i.StoreId == OtherStoreId)
                .Select(i => i.StoreInventoryId)
                .SingleAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var result = await service.ReportShortageAsync(StoreId, SalesStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = otherId,
                Note = "Cố gắng báo kho cửa hàng khác."
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync());
        }

        [Fact]
        public async Task Controller_UnauthorizedRole_Returns403()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 1m, min: 5m);
            SeedStaffWithRole(ctx, CustomerStaffId, 9005, RoleConstants.Customer, "cust@test.local");
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx, RoleConstants.Customer, CustomerStaffId);
            var invId = await IngredientInventoryIdAsync(ctx);
            var result = await controller.ReportShortage(new StockShortageReportRequestDto
            {
                StoreInventoryId = invId,
                Note = "Customer không được báo thiếu."
            });

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }

        [Fact]
        public async Task Recipients_StoreManagerAndAccountant_GetNotifications()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 1m, min: 5m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "sm@test.local");
            SeedStaffWithRole(ctx, AccountantStaffId, 9004, RoleConstants.AccountantWarehouse, "aw@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            await service.ReportShortageAsync(StoreId, SalesStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                Note = "Kiểm tra người nhận thông báo."
            });

            var recipientIds = await ctx.StaffNotifications
                .Select(n => n.RecipientStaffId)
                .OrderBy(id => id)
                .ToListAsync();
            Assert.Equal(new[] { ManagerStaffId, AccountantStaffId }.OrderBy(x => x), recipientIds);
        }

        [Fact]
        public async Task EmailFailure_DoesNotRollback_AndCountsFailed()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 0m, min: 5m);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            SeedStaffWithRole(ctx, ManagerStaffId, 9003, RoleConstants.StoreManager, "sm@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(throwOnSend: true).Object);
            var result = await service.ReportShortageAsync(StoreId, SalesStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                Note = "Email sẽ fail nhưng báo cáo vẫn lưu."
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());
            Assert.Equal(1, await ctx.StaffNotifications.CountAsync());
            Assert.True(result.Data!.EmailAttempted);
            Assert.Equal(0, result.Data.EmailSentCount);
            Assert.Equal(1, result.Data.EmailFailedCount);

            var n = await ctx.StaffNotifications.SingleAsync();
            Assert.True(n.EmailAttempted);
            Assert.False(n.EmailSent);
            Assert.False(string.IsNullOrWhiteSpace(n.EmailErrorSummary));
        }

        [Fact]
        public async Task ZeroRecipients_StillCreatesAlert_WithWarning()
        {
            using var ctx = CreateDbContext();
            SeedRolesAndInventory(ctx, ingredientQty: 2m, min: null);
            SeedStaffWithRole(ctx, SalesStaffId, 9001, RoleConstants.SalesStaff, "sales@test.local");
            // No SM / AW seeded
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var result = await service.ReportShortageAsync(StoreId, SalesStaffId, new StockShortageReportRequestDto
            {
                StoreInventoryId = await IngredientInventoryIdAsync(ctx),
                Note = "Không có quản lý nhưng vẫn ghi nhận.",
                Reason = "Cần bổ sung cho ca tiếp theo.",
                TargetStockBaseQuantity = 5m
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());
            Assert.Equal(0, result.Data!.NotificationCount);
            Assert.Contains(result.Data.Warnings, w => w.Contains("người nhận"));
        }

        // ---------- helpers ----------

        private static StockShortageReportService CreateService(
            CafeChain.Data.AppDbContext ctx,
            IEmailService email)
        {
            return new StockShortageReportService(
                ctx,
                email,
                new Mock<ILogger<StockShortageReportService>>().Object);
        }

        private static Mock<IEmailService> CreateEmailMock(bool throwOnSend)
        {
            var mock = new Mock<IEmailService>();
            mock.Setup(e => e.BuildStockShortageReportEmail(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.DateTime>()))
                .Returns("<html>shortage</html>");

            if (throwOnSend)
            {
                mock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new System.InvalidOperationException("SMTP unavailable"));
            }
            else
            {
                mock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            }

            return mock;
        }

        private static POSStockAlertController CreateController(
            CafeChain.Data.AppDbContext ctx,
            string role,
            int staffId)
        {
            var service = CreateService(ctx, CreateEmailMock(false).Object);
            var controller = new POSStockAlertController(service);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Role, role),
                        new Claim("StoreId", StoreId.ToString()),
                        new Claim("StaffId", staffId.ToString()),
                    }, "Test"))
                }
            };
            return controller;
        }

        private static void SeedRolesAndInventory(
            CafeChain.Data.AppDbContext ctx,
            decimal ingredientQty,
            decimal? min,
            decimal recipeQty = 5m,
            decimal ingredientReserved = 0m)
        {
            // Roles may already exist from EnsureCreated seed
            EnsureRole(ctx, 3, RoleConstants.StoreManager, true);
            EnsureRole(ctx, 4, RoleConstants.SalesStaff, true);
            EnsureRole(ctx, 5, RoleConstants.AccountantWarehouse, true);
            EnsureRole(ctx, 7, RoleConstants.Customer, false);
            EnsureRole(ctx, 8, RoleConstants.ShiftSupervisor, true);

            if (!ctx.Units.Any(u => u.UnitId == UnitId))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = UnitId,
                    UnitCode = "g",
                    Name = "Gram",
                    Active = true
                });
            }

            if (!ctx.Ingredients.Any(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING98",
                    Name = "Sữa test #98",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }

            if (!ctx.Recipes.Any(r => r.RecipeId == RecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "RCP98",
                    Name = "BTP test #98",
                    Active = true,
                    Status = "Active"
                });
            }

            if (!ctx.Stores.Any(s => s.StoreId == StoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "Store #98",
                    Address = "Test",
                    Phone = "0",
                    Active = true,
                    CreatedAt = System.DateTime.UtcNow
                });
            }
            if (!ctx.Stores.Any(s => s.StoreId == OtherStoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = OtherStoreId,
                    Name = "Store #98-B",
                    Address = "Test",
                    Phone = "0",
                    Active = true,
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = ingredientQty,
                ReservedQty = ingredientReserved,
                MinStockLevel = min,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });

            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                AvailableQty = recipeQty,
                ReservedQty = 0,
                MinStockLevel = min,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
        }

        private static void EnsureRole(CafeChain.Data.AppDbContext ctx, int roleId, string name, bool storeLevel)
        {
            if (ctx.Roles.Any(r => r.RoleId == roleId || r.Name == name))
                return;
            ctx.Roles.Add(new Role
            {
                RoleId = roleId,
                Name = name,
                Active = true,
                IsStoreLevel = storeLevel,
                CreatedAt = System.DateTime.UtcNow
            });
        }

        private static void SeedStaffWithRole(
            CafeChain.Data.AppDbContext ctx,
            int staffId,
            int accountId,
            string roleName,
            string email)
        {
            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? ctx.Roles.First(r => r.Name == roleName);

            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = System.DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole
            {
                AccountId = accountId,
                RoleId = role.RoleId
            });
            ctx.Staffs.Add(new Staff
            {
                StaffId = staffId,
                AccountId = accountId,
                StoreId = StoreId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = System.DateTime.UtcNow,
            });
        }

        private static async Task<int> IngredientInventoryIdAsync(CafeChain.Data.AppDbContext ctx) =>
            await ctx.StoreInventories
                .Where(i => i.StoreId == StoreId && i.IngredientId == IngredientId)
                .Select(i => i.StoreInventoryId)
                .SingleAsync();

        private static async Task<int> RecipeInventoryIdAsync(CafeChain.Data.AppDbContext ctx) =>
            await ctx.StoreInventories
                .Where(i => i.StoreId == StoreId && i.RecipeId == RecipeId)
                .Select(i => i.StoreInventoryId)
                .SingleAsync();
    }
}
