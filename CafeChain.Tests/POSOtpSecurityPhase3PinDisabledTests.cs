using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Staffs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 3/4 (#140/#143): legacy PIN stack fully removed — zero executable PIN residue.
    /// </summary>
    public class POSOtpSecurityPhase3PinDisabledTests
    {
        [Fact]
        public void SupervisorApproval_NoStaffPinHashProperty()
        {
            Assert.Null(typeof(Staff).GetProperty("PinHash"));
            Assert.Null(typeof(Staff).GetProperty("SupervisorPin"));
            Assert.Null(typeof(Staff).GetProperty("Pin"));
        }

        [Fact]
        public void SupervisorApproval_NoPinVerificationServiceRegistered()
        {
            Assert.Null(Type.GetType("CafeChain.Application.Interfaces.POS.ISupervisorAuthService, CafeChain"));
            Assert.Null(Type.GetType("CafeChain.Application.Services.POS.SupervisorAuthService, CafeChain"));
            Assert.Null(Type.GetType("CafeChain.Infrastructure.Interfaces.Admin.POS.ISupervisorRepository, CafeChain"));
            Assert.Null(Type.GetType("CafeChain.Infrastructure.Repositories.Admin.POS.SupervisorRepository, CafeChain"));

            var cafeAssembly = typeof(WorkShiftService).Assembly;
            Assert.DoesNotContain(cafeAssembly.GetTypes(), t => t.Name is "ISupervisorAuthService" or "SupervisorAuthService");
            Assert.DoesNotContain(cafeAssembly.GetTypes(), t => t.Name is "ISupervisorRepository" or "SupervisorRepository");
        }

        [Fact]
        public void SupervisorApproval_NoPinRepositoryQuery()
        {
            var otpRepo = typeof(IOtpChallengeRepository);
            Assert.Null(otpRepo.GetMethod("GetSupervisorsWithPinAsync"));
            Assert.Null(otpRepo.GetMethod("FindSupervisorByPin"));

            var cafeAssembly = typeof(WorkShiftService).Assembly;
            foreach (var type in cafeAssembly.GetTypes().Where(t => t.IsInterface || t.IsClass))
            {
                Assert.Null(type.GetMethod("GetSupervisorsWithPinAsync"));
                Assert.Null(type.GetMethod("AuthorizePinAsync"));
                Assert.Null(type.GetMethod("VerifySupervisorPinAsync"));
                Assert.Null(type.GetMethod("UpdatePinAsync"));
            }
        }

        [Fact]
        public void SupervisorApproval_NoUpdatePinEndpoint()
        {
            var attendance = typeof(AttendanceController);
            Assert.Null(attendance.GetMethod("UpdatePin"));
            Assert.Null(attendance.GetMethod("AuthorizeBypass"));

            var adminPos = typeof(AdminPOSController);
            Assert.Null(adminPos.GetMethod("AuthorizeSupervisor"));

            var security = typeof(IAttendanceSecurityService);
            Assert.Null(security.GetMethod("UpdatePinAsync"));
        }

        [Fact]
        public void SupervisorApproval_NoLegacyPinDtoField()
        {
            var cafeAssembly = typeof(WorkShiftService).Assembly;
            var dtoTypes = cafeAssembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.Contains("DTOs") && t.IsClass)
                .ToList();

            foreach (var dto in dtoTypes)
            {
                Assert.Null(dto.GetProperty("SupervisorPin"));
                Assert.Null(dto.GetProperty("SupervisorPinCode"));
                Assert.Null(dto.GetProperty("PinCode"));
                // Generic "Pin" field (BypassAuthorizationRequest / SupervisorAuthRequestDto removed)
                Assert.Null(dto.GetProperty("Pin"));
            }
        }

        [Fact]
        public void SupervisorApproval_NoPinManagementUi()
        {
            var root = FindRepoRoot();
            var staffHub = File.ReadAllText(Path.Combine(root, "CafeChain", "Views", "StaffHub", "Index.cshtml"));
            Assert.DoesNotContain("Đổi mã PIN", staffHub, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("openPinModal", staffHub);
            Assert.DoesNotContain("txtNewPin", staffHub);
            Assert.DoesNotContain("/api/Attendance/UpdatePin", staffHub);
            Assert.DoesNotContain("Nhập PIN", staffHub, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SupervisorApproval_NoActivePinJavaScriptHandler()
        {
            var root = FindRepoRoot();
            var posApp = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "pos-app.js"));
            Assert.DoesNotContain("function openPinModal", posApp);
            Assert.DoesNotContain("function submitPin", posApp);
            Assert.DoesNotContain("function pinInput", posApp);
            Assert.DoesNotContain("AuthorizeSupervisor", posApp);
            Assert.DoesNotContain("pinValue", posApp);
            Assert.DoesNotContain("pinResolve", posApp);

            var adminPosView = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminPOS", "Index.cshtml"));
            Assert.DoesNotContain("pin-numpad", adminPosView);
            Assert.DoesNotContain("btnPinConfirm", adminPosView);
            Assert.DoesNotContain("submitPin()", adminPosView);
            Assert.DoesNotContain("Xác thực Trưởng ca", adminPosView);
            Assert.DoesNotContain("mã PIN 4 số", adminPosView, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SupervisorApproval_InvoiceAuditLogIsNotAuthorization()
        {
            var root = FindRepoRoot();
            var workShift = File.ReadAllText(Path.Combine(root, "CafeChain", "Application", "Services", "POS", "WorkShiftService.cs"));
            Assert.DoesNotContain("GetPendingAuditLogAsync", workShift);
            Assert.DoesNotContain("AuthorizePinAsync", workShift);
            Assert.DoesNotContain("VerifySupervisorPinAsync", workShift);
            Assert.DoesNotContain("ISupervisorAuthService", workShift);
        }

        [Fact]
        public void SupervisorApproval_UsesOtpChallengeOnly()
        {
            var ctor = typeof(WorkShiftService).GetConstructors().Single();
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();
            Assert.Contains(typeof(IOtpChallengeRepository), paramTypes);
            Assert.DoesNotContain(paramTypes, t => t.Name.Contains("SupervisorAuth", StringComparison.Ordinal));
            Assert.DoesNotContain(paramTypes, t => t.Name.Contains("Pin", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SupervisorApproval_AttendanceControllerHasNoPinDependencies()
        {
            var ctor = typeof(AttendanceController).GetConstructors().Single();
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();
            Assert.Equal(2, paramTypes.Count);
            Assert.Contains(typeof(IAttendanceSecurityService), paramTypes);
            Assert.Contains(typeof(IAttendanceActionService), paramTypes);
            Assert.DoesNotContain(paramTypes, t => t.Name.Contains("Supervisor", StringComparison.Ordinal));
        }

        [Fact]
        public void SupervisorApproval_AdminPosControllerHasNoPinDependencies()
        {
            var ctor = typeof(AdminPOSController).GetConstructors().Single();
            Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType.Name.Contains("Supervisor", StringComparison.Ordinal));
            Assert.DoesNotContain(ctor.GetParameters(), p => p.Name?.Contains("Pin", StringComparison.OrdinalIgnoreCase) == true);
        }

        [Fact]
        public void SupervisorApproval_DiDoesNotRegisterPinServices()
        {
            var root = FindRepoRoot();
            var appExt = File.ReadAllText(Path.Combine(root, "CafeChain", "Extensions", "Services", "ApplicationServiceExtensions.cs"));
            var repoExt = File.ReadAllText(Path.Combine(root, "CafeChain", "Extensions", "Services", "RepositoryServiceExtensions.cs"));
            Assert.DoesNotContain("ISupervisorAuthService", appExt);
            Assert.DoesNotContain("SupervisorAuthService", appExt);
            Assert.DoesNotContain("ISupervisorRepository", repoExt);
            Assert.DoesNotContain("SupervisorRepository", repoExt);
        }

        // ---- helpers ----

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

            throw new InvalidOperationException("Could not locate repository root from test base directory.");
        }
    }
}
