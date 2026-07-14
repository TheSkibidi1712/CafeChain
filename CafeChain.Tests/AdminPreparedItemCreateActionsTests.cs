using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Helpers;
using CafeChain.ViewModels.Admin.PreparedItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// PreparedItem Index create CTAs + write permission contracts
    /// (regression after #129 typed VM / empty-state partial).
    /// </summary>
    public class AdminPreparedItemCreateActionsTests
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CafeChain.slnx"))
                    || File.Exists(Path.Combine(dir.FullName, "CafeChain", "CafeChain.csproj")))
                    return dir.FullName.EndsWith("CafeChain", StringComparison.OrdinalIgnoreCase)
                           && File.Exists(Path.Combine(dir.FullName, "CafeChain.csproj"))
                        ? dir.Parent!.FullName
                        : dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static string ReadIndexView()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminPreparedItem", "Index.cshtml");
            Assert.True(File.Exists(path), path);
            return File.ReadAllText(path);
        }

        private static AdminPreparedItemController CreateController(string role, out Mock<IAdminPreparedItemService> mock)
        {
            mock = new Mock<IAdminPreparedItemService>(MockBehavior.Strict);
            mock.Setup(s => s.GetPagedAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((new List<AdminPreparedItemDTO>(), 0));

            var controller = new AdminPreparedItemController(mock.Object);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Name, "tester"),
                new(ClaimTypes.Email, "t@test.local"),
                new(ClaimTypes.Role, role),
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
                }
            };
            return controller;
        }

        private static async Task<AdminPreparedItemIndexPageVM> InvokeIndexAsync(string role)
        {
            var controller = CreateController(role, out _);
            var result = await controller.Index(null, null, 1);
            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<AdminPreparedItemIndexPageVM>(view.Model);
            return vm;
        }

        [Fact]
        public void AdminPreparedItem_Index_WriteRole_ShowsCreateButton()
        {
            var html = ReadIndexView();
            Assert.Contains("id=\"btnCreate\"", html, StringComparison.Ordinal);
            Assert.Contains("Tạo bán thành phẩm", html, StringComparison.Ordinal);
            // Header CTA must stay behind the same canWrite gate as empty-state CTA.
            Assert.Contains("@if (canWrite)", html, StringComparison.Ordinal);
            var btnCreateIdx = html.IndexOf("id=\"btnCreate\"", StringComparison.Ordinal);
            var canWriteBefore = html.LastIndexOf("@if (canWrite)", btnCreateIdx, StringComparison.Ordinal);
            Assert.True(canWriteBefore >= 0, "btnCreate must be inside @if (canWrite)");
        }

        [Fact]
        public void AdminPreparedItem_Index_WriteRole_EmptyStateShowsCreateFirstCta()
        {
            var html = ReadIndexView();
            Assert.Contains("preparedItemEmptyState", html, StringComparison.Ordinal);
            Assert.Contains("Tạo bán thành phẩm đầu tiên", html, StringComparison.Ordinal);
            Assert.Contains("id=\"btnCreateEmpty\"", html, StringComparison.Ordinal);
            Assert.Contains("Chưa có bán thành phẩm", html, StringComparison.Ordinal);
            // Empty CTA must be explicit (not only ValueTuple partial) and gated on canWrite.
            Assert.DoesNotContain("partial name=\"_EmptyState\"", html, StringComparison.Ordinal);
            var emptyCtaIdx = html.IndexOf("id=\"btnCreateEmpty\"", StringComparison.Ordinal);
            var canWriteBefore = html.LastIndexOf("@if (canWrite)", emptyCtaIdx, StringComparison.Ordinal);
            Assert.True(canWriteBefore >= 0, "btnCreateEmpty must be inside @if (canWrite)");
        }

        [Fact]
        public void AdminPreparedItem_Index_ReadOnlyRole_HidesCreateActions()
        {
            var html = ReadIndexView();
            // Read-only empty copy present; create CTAs still exist in markup but only under canWrite.
            Assert.Contains("Chỉ Quản trị hệ thống, Chủ doanh nghiệp hoặc Kế toán/kho", html, StringComparison.Ordinal);

            // Controller: panel-read roles must not get CanWrite.
            foreach (var role in new[]
                     {
                         RoleConstants.StoreManager,
                         RoleConstants.AreaManager,
                         RoleConstants.SalesStaff,
                         RoleConstants.ShiftSupervisor
                     })
            {
                Assert.False(RoleHelper.CanWritePreparedItems(
                    new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"))),
                    role);
            }
        }

        [Fact]
        public void AdminPreparedItem_Create_UnauthorizedRole_IsRejected()
        {
            var method = typeof(AdminPreparedItemController).GetMethod(nameof(AdminPreparedItemController.Create));
            Assert.NotNull(method);
            var auth = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(auth);
            Assert.Equal(RoleHelper.PreparedItemWriteRoles, auth!.Roles);

            Assert.Contains(RoleConstants.SystemAdmin, auth.Roles, StringComparison.Ordinal);
            Assert.Contains(RoleConstants.BusinessOwner, auth.Roles, StringComparison.Ordinal);
            Assert.Contains(RoleConstants.AccountantWarehouse, auth.Roles, StringComparison.Ordinal);
            Assert.DoesNotContain(RoleConstants.StoreManager, auth.Roles, StringComparison.Ordinal);
            Assert.DoesNotContain(RoleConstants.AreaManager, auth.Roles, StringComparison.Ordinal);
            Assert.DoesNotContain(RoleConstants.SalesStaff, auth.Roles, StringComparison.Ordinal);
            Assert.DoesNotContain(RoleConstants.ShiftSupervisor, auth.Roles, StringComparison.Ordinal);
            Assert.DoesNotContain(RoleConstants.Customer, auth.Roles, StringComparison.Ordinal);

            // UI helper stays aligned with server Authorize surface.
            Assert.False(RoleHelper.CanWritePreparedItems(
                new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Role, RoleConstants.StoreManager)
                }, "Test"))));
        }

        [Fact]
        public async Task AdminPreparedItem_Index_PageModel_CarriesCanWritePermission()
        {
            foreach (var writeRole in new[]
                     {
                         RoleConstants.SystemAdmin,
                         RoleConstants.BusinessOwner,
                         RoleConstants.AccountantWarehouse
                     })
            {
                var vm = await InvokeIndexAsync(writeRole);
                Assert.True(vm.CanWrite, $"Expected CanWrite for {writeRole}");
                Assert.Empty(vm.Items);
            }

            foreach (var readRole in new[]
                     {
                         RoleConstants.StoreManager,
                         RoleConstants.AreaManager
                     })
            {
                var vm = await InvokeIndexAsync(readRole);
                Assert.False(vm.CanWrite, $"Expected !CanWrite for {readRole}");
            }
        }
    }
}
