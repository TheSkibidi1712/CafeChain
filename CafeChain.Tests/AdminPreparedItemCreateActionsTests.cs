using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Results;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.ViewModels.Admin.PreparedItems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
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

        private static AdminPreparedItemController CreateController(
            string role,
            IReadOnlyCollection<string> allowedPermissions,
            out Mock<IAdminPreparedItemService> mock)
        {
            mock = new Mock<IAdminPreparedItemService>(MockBehavior.Strict);
            mock.Setup(s => s.GetPagedAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((new List<AdminPreparedItemDTO>(), 0));

            var controller = new AdminPreparedItemController(mock.Object);
            var permissions = new Mock<IAdminPermissionService>();
            permissions.Setup(x => x.HasPermissionAsync(1, It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((int _, string code, int? _) =>
                    ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                    {
                        AccountId = 1,
                        PermissionCode = code,
                        Allowed = allowedPermissions.Contains(code)
                    }));
            var services = new ServiceCollection()
                .AddSingleton(permissions.Object)
                .BuildServiceProvider();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Name, "tester"),
                new(ClaimTypes.Email, "t@test.local"),
                new(ClaimTypes.Role, role),
            };
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
                RequestServices = services
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private static async Task<AdminPreparedItemIndexPageVM> InvokeIndexAsync(
            string role,
            params string[] allowedPermissions)
        {
            var controller = CreateController(role, allowedPermissions, out _);
            var result = await controller.Index(null, null, 1);
            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<AdminPreparedItemIndexPageVM>(view.Model);
            return vm;
        }

        [Fact]
        public void AdminPreparedItem_Index_WriteRole_ShowsCreateButton()
        {
            var html = ReadIndexView();
            Assert.Contains("var heroActions = canWrite", html, StringComparison.Ordinal);
            Assert.Contains("Id = \"btnCreate\"", html, StringComparison.Ordinal);
            Assert.Contains("Tạo bán thành phẩm", html, StringComparison.Ordinal);
            Assert.Contains("Actions = heroActions", html, StringComparison.Ordinal);
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
            Assert.Contains("Tài khoản hiện tại chỉ có quyền xem", html, StringComparison.Ordinal);
            Assert.DoesNotContain("RoleConstants", html, StringComparison.Ordinal);
            Assert.DoesNotContain("User.IsInRole", html, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminPreparedItem_Create_RequiresPermissionWithoutRoleAllowList()
        {
            var method = typeof(AdminPreparedItemController).GetMethod(nameof(AdminPreparedItemController.Create));
            Assert.NotNull(method);
            var auth = method!.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(auth);
            Assert.Equal(
                RequirePermissionAttribute.PolicyPrefix + PermissionConstants.PreparedItemCreate,
                auth!.Policy);
            Assert.Null(auth.Roles);
        }

        [Fact]
        public async Task AdminPreparedItem_Index_PageModel_CarriesCanWritePermission()
        {
            foreach (var role in new[]
                     {
                         RoleConstants.SystemAdmin,
                         RoleConstants.StoreManager
                     })
            {
                var vm = await InvokeIndexAsync(role, PermissionConstants.PreparedItemCreate);
                Assert.True(vm.CanWrite, $"Expected CanWrite from effective permission for {role}");
                Assert.Empty(vm.Items);
            }

            foreach (var role in new[]
                     {
                         RoleConstants.BusinessOwner,
                         RoleConstants.AccountantWarehouse
                     })
            {
                var vm = await InvokeIndexAsync(role);
                Assert.False(vm.CanWrite, $"Expected override-denied capability for {role}");
            }
        }
    }
}
