using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Operations;
using CafeChain.Controllers.Api.v1;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #101 — StaffNotification read/mark isolation.</summary>
    public class POSNotificationsIssue101Tests : IntegrationTestBase
    {
        private const int StoreId = 90;
        private const int StaffA = 901;
        private const int StaffB = 902;

        [Fact]
        public async Task UnreadCount_OnlyCurrentStaff()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var service = new StaffNotificationQueryService(ctx);
            var a = await service.GetUnreadCountAsync(StaffA);
            var b = await service.GetUnreadCountAsync(StaffB);

            Assert.True(a.IsSuccess);
            Assert.True(b.IsSuccess);
            Assert.Equal(2, a.Data!.UnreadCount);
            Assert.Equal(1, b.Data!.UnreadCount);
        }

        [Fact]
        public async Task List_OnlyCurrentStaff_AndPagination()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            // Extra for A
            for (var i = 0; i < 5; i++)
                AddNotification(ctx, StaffA, $"Extra {i}", isRead: false);
            await ctx.SaveChangesAsync();

            var service = new StaffNotificationQueryService(ctx);
            var page1 = await service.GetListAsync(StaffA, 1, 3, StaffNotificationQueryService.ChannelPos);

            Assert.True(page1.IsSuccess);
            Assert.Equal(3, page1.Data!.Items.Count);
            // Staff A seed: 2 unread + 1 read = 3, plus 5 extra unread = 8
            Assert.Equal(8, page1.Data.Total);
            Assert.All(page1.Data.Items, i => Assert.True(i.NotificationId > 0));
            Assert.DoesNotContain(page1.Data.Items, i => i.Title.Contains("Staff B"));
        }

        [Fact]
        public async Task MarkRead_Own_Works()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var id = await ctx.StaffNotifications
                .Where(n => n.RecipientStaffId == StaffA && !n.IsRead)
                .Select(n => n.StaffNotificationId)
                .FirstAsync();

            var service = new StaffNotificationQueryService(ctx);
            var result = await service.MarkReadAsync(StaffA, id);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.MarkedCount);
            var row = await ctx.StaffNotifications.SingleAsync(n => n.StaffNotificationId == id);
            Assert.True(row.IsRead);
            Assert.NotNull(row.ReadAt);
        }

        [Fact]
        public async Task MarkRead_OtherStaff_Rejected()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var bId = await ctx.StaffNotifications
                .Where(n => n.RecipientStaffId == StaffB)
                .Select(n => n.StaffNotificationId)
                .FirstAsync();

            var service = new StaffNotificationQueryService(ctx);
            var result = await service.MarkReadAsync(StaffA, bId);

            Assert.False(result.IsSuccess);
            Assert.False(await ctx.StaffNotifications.AnyAsync(n =>
                n.StaffNotificationId == bId && n.IsRead));
        }

        [Fact]
        public async Task ReadAll_OnlyCurrentStaff()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var service = new StaffNotificationQueryService(ctx);
            var result = await service.MarkAllReadAsync(StaffA);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.MarkedCount);
            Assert.Equal(0, await ctx.StaffNotifications.CountAsync(n =>
                n.RecipientStaffId == StaffA && !n.IsRead));
            Assert.Equal(1, await ctx.StaffNotifications.CountAsync(n =>
                n.RecipientStaffId == StaffB && !n.IsRead));
        }

        [Fact]
        public async Task Response_DoesNotExposeRawEmailError_AndMapsTargetUrl()
        {
            using var ctx = CreateDbContext();
            EnsureStore(ctx);
            EnsureStaff(ctx, StaffA);
            ctx.StaffNotifications.Add(new StaffNotification
            {
                StoreId = StoreId,
                RecipientStaffId = StaffA,
                Type = StaffNotificationTypes.StockShortageReport,
                Title = "Báo thiếu",
                Body = "Body",
                EntityType = StaffNotificationEntityTypes.StockAlert,
                EntityId = 99,
                IsRead = false,
                CreatedAt = System.DateTime.UtcNow,
                EmailAttempted = true,
                EmailSent = false,
                EmailErrorSummary = "SmtpException: SECRET_PASSWORD_LEAK"
            });
            await ctx.SaveChangesAsync();

            var service = new StaffNotificationQueryService(ctx);
            var list = await service.GetListAsync(StaffA, 1, 20, StaffNotificationQueryService.ChannelPos);

            Assert.True(list.IsSuccess);
            var item = Assert.Single(list.Data!.Items);
            Assert.Equal("failed", item.EmailDeliveryHint);
            Assert.Equal("/inventory", item.TargetUrl);
            // DTO has no EmailErrorSummary property — ensure body/title don't contain secret
            Assert.DoesNotContain("SECRET", item.Body);
            Assert.DoesNotContain("SECRET", item.Title);
        }

        [Fact]
        public void MapTargetUrl_AdminChannel_NullForStockAlert()
        {
            var url = StaffNotificationQueryService.MapTargetUrl(
                StaffNotificationEntityTypes.StockAlert,
                StaffNotificationTypes.StockShortageReport,
                StaffNotificationQueryService.ChannelAdmin);
            Assert.Null(url);
        }

        [Fact]
        public async Task Controller_List_UsesJwtStaffId()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx, StaffA);
            var result = await controller.GetList(1, 20);
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task Controller_MarkRead_OtherStaff_NotFound()
        {
            using var ctx = CreateDbContext();
            SeedTwoStaffNotifications(ctx);
            await ctx.SaveChangesAsync();

            var bId = await ctx.StaffNotifications
                .Where(n => n.RecipientStaffId == StaffB)
                .Select(n => n.StaffNotificationId)
                .FirstAsync();

            var controller = CreateController(ctx, StaffA);
            var result = await controller.MarkRead(bId);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        private static POSNotificationsController CreateController(
            CafeChain.Data.AppDbContext ctx,
            int staffId)
        {
            var service = new StaffNotificationQueryService(ctx);
            var controller = new POSNotificationsController(service);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("StaffId", staffId.ToString()),
                        new Claim("StoreId", StoreId.ToString()),
                        new Claim(ClaimTypes.Role, RoleConstants.SalesStaff),
                    }, "Test"))
                }
            };
            return controller;
        }

        private static void SeedTwoStaffNotifications(CafeChain.Data.AppDbContext ctx)
        {
            EnsureStore(ctx);
            EnsureStaff(ctx, StaffA);
            EnsureStaff(ctx, StaffB);

            AddNotification(ctx, StaffA, "Staff A unread 1", isRead: false);
            AddNotification(ctx, StaffA, "Staff A unread 2", isRead: false);
            AddNotification(ctx, StaffB, "Staff B only", isRead: false);
            AddNotification(ctx, StaffA, "Staff A read", isRead: true);
        }

        private static void AddNotification(
            CafeChain.Data.AppDbContext ctx,
            int staffId,
            string title,
            bool isRead)
        {
            ctx.StaffNotifications.Add(new StaffNotification
            {
                StoreId = StoreId,
                RecipientStaffId = staffId,
                Type = StaffNotificationTypes.StockShortageReport,
                Title = title,
                Body = $"Body for {title}",
                EntityType = StaffNotificationEntityTypes.StockAlert,
                EntityId = 1,
                IsRead = isRead,
                ReadAt = isRead ? System.DateTime.UtcNow : null,
                CreatedAt = System.DateTime.UtcNow.AddMinutes(-staffId),
                EmailAttempted = false,
                EmailSent = false
            });
        }

        private static void EnsureStore(CafeChain.Data.AppDbContext ctx)
        {
            if (ctx.Stores.Any(s => s.StoreId == StoreId)) return;
            ctx.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Store 101",
                Address = "x",
                Phone = "0",
                Active = true,
                CreatedAt = System.DateTime.UtcNow
            });
        }

        private static void EnsureStaff(CafeChain.Data.AppDbContext ctx, int staffId)
        {
            if (ctx.Staffs.Any(s => s.StaffId == staffId)) return;
            var accountId = 10000 + staffId;
            ctx.Accounts.Add(new CafeChain.Models.Customers.Account
            {
                AccountId = accountId,
                Email = $"s{staffId}@test.local",
                PasswordHash = "x",
                Active = true,
                CreatedAt = System.DateTime.UtcNow
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
    }
}
