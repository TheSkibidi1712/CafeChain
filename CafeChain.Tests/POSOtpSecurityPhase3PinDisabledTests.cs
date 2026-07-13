using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.Attendance;
using CafeChain.Application.Services.POS;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Interfaces.Attendance;
using CafeChain.Models.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 3 (#140): Attendance + legacy AdminPOS PIN bypass/management disabled.
    /// Generic approval bool is not migratable — endpoints return FEATURE_NOT_AVAILABLE.
    /// </summary>
    public class POSOtpSecurityPhase3PinDisabledTests
    {
        private const string FeatureNotAvailable = OtpConstants.ErrorCodes.FeatureNotAvailable;

        [Fact]
        public async Task AttendanceBypass_Endpoint_IsFeatureNotAvailable()
        {
            var controller = CreateAttendanceController();
            var result = await controller.AuthorizeBypass(new BypassAuthorizationRequest
            {
                Pin = "1234",
                ActionName = "OPEN_SHIFT_LATE",
                TargetId = 1,
                Reason = "test"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            AssertErrorPayload(bad.Value, FeatureNotAvailable);
        }

        [Fact]
        public async Task AttendanceBypass_PinPayload_IsRejected()
        {
            var controller = CreateAttendanceController();
            var result = await controller.AuthorizeBypass(new BypassAuthorizationRequest
            {
                Pin = "9999",
                ActionName = "VOID_INVOICE",
                Reason = "x"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            AssertErrorPayload(bad.Value, FeatureNotAvailable);
        }

        [Fact]
        public async Task AttendanceBypass_DoesNotCreateAuditOrMutation()
        {
            var repo = new Mock<ISupervisorRepository>(MockBehavior.Strict);
            var service = new SupervisorAuthService(repo.Object, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.AuthorizePinAsync("1234", 1, 3, "VOID_INVOICE", 99, "reason");
            Assert.False(result.IsSuccess);
            Assert.Equal(FeatureNotAvailable, result.ErrorCode);
            repo.Verify(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>()), Times.Never);
        }

        [Fact]
        public async Task AttendanceUpdatePin_IsFeatureNotAvailable()
        {
            var security = new Mock<IAttendanceSecurityService>(MockBehavior.Strict);
            security.Setup(s => s.UpdatePinAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(CafeChain.Application.Results.ServiceResult.Failure(
                    OtpConstants.PinDisabledMessages.UpdatePin,
                    errorCode: FeatureNotAvailable));

            var controller = CreateAttendanceController(security: security.Object);
            var result = await controller.UpdatePin("1234");

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            AssertErrorPayload(bad.Value, FeatureNotAvailable);
            security.Verify(s => s.UpdatePinAsync(It.IsAny<int>(), "1234"), Times.Once);
        }

        [Fact]
        public async Task AttendanceSecurityService_UpdatePin_DoesNotMutatePinHash()
        {
            var repo = new Mock<IAttendanceRepository>(MockBehavior.Strict);
            var service = new AttendanceSecurityService(repo.Object);

            var result = await service.UpdatePinAsync(accountId: 1, pin: "1234");
            Assert.False(result.IsSuccess);
            Assert.Equal(FeatureNotAvailable, result.ErrorCode);
        }

        [Fact]
        public void AdminPos_AuthorizeSupervisor_IsFeatureNotAvailable()
        {
            var controller = CreateAdminPosController();
            var result = controller.AuthorizeSupervisor(new SupervisorAuthRequestDto
            {
                Pin = "1234",
                ActionName = "PRICE_OVERRIDE",
                TargetId = 1,
                Reason = "test"
            });

            var json = Assert.IsType<JsonResult>(result);
            AssertErrorPayload(json.Value, FeatureNotAvailable);
            var successProp = json.Value!.GetType().GetProperty("success");
            Assert.NotNull(successProp);
            Assert.Equal(false, successProp!.GetValue(json.Value));
        }

        [Fact]
        public void AdminPos_PinPayload_DoesNotCreateAudit()
        {
            var repo = new Mock<ISupervisorRepository>(MockBehavior.Strict);
            var service = new SupervisorAuthService(repo.Object, new MemoryCache(new MemoryCacheOptions()));
            var result = service.AuthorizePinAsync("0000", 10, 3, "VOID_INVOICE", 5, "r").GetAwaiter().GetResult();
            Assert.False(result.IsSuccess);
            repo.Verify(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>()), Times.Never);
        }

        [Fact]
        public void AdminPos_GenericApprovalBool_IsRemoved()
        {
            var controller = CreateAdminPosController();
            var result = controller.AuthorizeSupervisor(new SupervisorAuthRequestDto
            {
                Pin = "1234",
                ActionName = "VOID_INVOICE",
                TargetId = 1,
                Reason = "x"
            });
            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal(false, json.Value!.GetType().GetProperty("success")!.GetValue(json.Value));
            AssertErrorPayload(json.Value, FeatureNotAvailable);
        }

        [Fact]
        public async Task SupervisorPin_VerifySupervisorPinAsync_IsFeatureNotAvailable()
        {
            var repo = new Mock<ISupervisorRepository>(MockBehavior.Strict);
            var service = new SupervisorAuthService(repo.Object, new MemoryCache(new MemoryCacheOptions()));
            var result = await service.VerifySupervisorPinAsync("1234", storeId: 3);
            Assert.False(result.IsSuccess);
            Assert.Equal(FeatureNotAvailable, result.ErrorCode);
            repo.Verify(r => r.GetSupervisorsWithPinAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void SupervisorPin_LegacyModal_IsRemoved()
        {
            var root = FindRepoRoot();
            var adminPosView = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminPOS", "Index.cshtml"));
            Assert.DoesNotContain("Xác thực Trưởng ca", adminPosView);
            Assert.DoesNotContain("mã PIN 4 số của Trưởng ca", adminPosView);
            Assert.DoesNotContain("pin-numpad", adminPosView);
            Assert.Contains("PIN AUTH MODAL removed", adminPosView);

            var staffHub = File.ReadAllText(Path.Combine(root, "CafeChain", "Views", "StaffHub", "Index.cshtml"));
            Assert.DoesNotContain("Đổi mã PIN (Ca trưởng)", staffHub);
            Assert.DoesNotContain("txtNewPin", staffHub);
            Assert.DoesNotContain("Cập nhật mã PIN", staffHub);

            var posApp = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "pos-app.js"));
            Assert.Contains("PIN AUTH — DISABLED", posApp);
            Assert.Contains("FEATURE_NOT_AVAILABLE", posApp);
        }

        [Fact]
        public void SupervisorPin_UpdateUi_IsNotAvailable()
        {
            var root = FindRepoRoot();
            var staffHub = File.ReadAllText(Path.Combine(root, "CafeChain", "Views", "StaffHub", "Index.cshtml"));
            Assert.DoesNotContain("onclick=\"openPinModal()\"", staffHub);
            Assert.DoesNotContain("/api/Attendance/UpdatePin", staffHub);
        }

        [Fact]
        public void InvoiceAuditLog_IsNotUsedAsAuthorizationAuthority()
        {
            var root = FindRepoRoot();
            var workShift = File.ReadAllText(Path.Combine(root, "CafeChain", "Application", "Services", "POS", "WorkShiftService.cs"));
            Assert.DoesNotContain("GetPendingAuditLogAsync", workShift);

            var auth = File.ReadAllText(Path.Combine(root, "CafeChain", "Application", "Services", "POS", "SupervisorAuthService.cs"));
            Assert.Contains("FeatureNotAvailable", auth);
            var authorizeMethod = ExtractMethod(auth, "AuthorizePinAsync");
            Assert.DoesNotContain("CreateAuditLogAsync", authorizeMethod);
        }

        [Fact]
        public void SupervisorPin_ActiveAttendanceCaller_DoesNotExist()
        {
            var root = FindRepoRoot();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "CafeChain"), "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".js") || f.EndsWith(".cshtml") || f.EndsWith(".tsx") || f.EndsWith(".ts")))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                    continue;
                var text = File.ReadAllText(file);
                Assert.False(
                    text.Contains("AuthorizeBypass") || text.Contains("/api/Attendance/AuthorizeBypass"),
                    $"Unexpected AuthorizeBypass reference in {file}");
            }
        }

        [Fact]
        public void SupervisorPin_ActiveAdminPosCaller_DoesNotExist()
        {
            var root = FindRepoRoot();
            var posApp = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "pos-app.js"));
            Assert.DoesNotContain("url: '/Admin/AdminPOS/AuthorizeSupervisor'", posApp);
            Assert.DoesNotContain("url: \"/Admin/AdminPOS/AuthorizeSupervisor\"", posApp);
        }

        [Fact]
        public void SupervisorPin_NonEmptyLegacyPayload_IsRejected()
        {
            var service = new SupervisorAuthService(
                Mock.Of<ISupervisorRepository>(),
                new MemoryCache(new MemoryCacheOptions()));
            var r1 = service.AuthorizePinAsync("1234", 1, 1, "X", 0, "r").GetAwaiter().GetResult();
            var r2 = service.VerifySupervisorPinAsync("1234", 1).GetAwaiter().GetResult();
            Assert.Equal(FeatureNotAvailable, r1.ErrorCode);
            Assert.Equal(FeatureNotAvailable, r2.ErrorCode);
        }

        [Fact]
        public void AdminPos_PinModal_IsNotRendered()
        {
            var root = FindRepoRoot();
            var adminPosView = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminPOS", "Index.cshtml"));
            Assert.DoesNotContain("btnPinConfirm", adminPosView);
            Assert.DoesNotContain("submitPin()", adminPosView);
        }

        // ---- helpers ----

        private static AttendanceController CreateAttendanceController(
            IAttendanceSecurityService? security = null)
        {
            var action = new Mock<IAttendanceActionService>(MockBehavior.Loose);
            var sec = security ?? Mock.Of<IAttendanceSecurityService>();
            var supervisor = Mock.Of<ISupervisorAuthService>();
            var controller = new AttendanceController(sec, action.Object, supervisor);
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"),
            }, "Test"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claims }
            };
            return controller;
        }

        private static AdminPOSController CreateAdminPosController()
        {
            return new AdminPOSController(
                Mock.Of<IWorkShiftService>(),
                Mock.Of<IPOSOrderService>(),
                Mock.Of<ISupervisorAuthService>(),
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<IInventoryDeductionService>());
        }

        private static void AssertErrorPayload(object? value, string expectedCode)
        {
            Assert.NotNull(value);
            var type = value!.GetType();
            var prop = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.Name.Equals("errorCode", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(prop);
            Assert.Equal(expectedCode, prop!.GetValue(value)?.ToString());
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "CafeChain"))
                    && Directory.Exists(Path.Combine(dir.FullName, "CafeChain.Tests")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "CafeChain"))
                    && Directory.Exists(Path.Combine(dir.FullName, "CafeChain.Tests")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate repo root for file assertions.");
        }

        private static string ExtractMethod(string source, string methodName)
        {
            var idx = source.IndexOf(methodName, StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Method {methodName} not found");
            var brace = source.IndexOf('{', idx);
            var depth = 0;
            for (var i = brace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(idx, i - idx + 1);
                }
            }
            return source.Substring(idx);
        }
    }
}
