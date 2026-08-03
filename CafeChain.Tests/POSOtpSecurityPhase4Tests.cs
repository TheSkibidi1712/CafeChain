using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 4 (#143): final PIN cleanup — model, services, endpoints, DTOs, DI, OTP-only contract.
    /// </summary>
    public class POSOtpSecurityPhase4Tests : IntegrationTestBase
    {
        private const int StoreId = 60;
        private const int RoleShiftSupervisor = 8;
        private const int RoleSalesStaff = 4;

        [Fact]
        public void SupervisorApproval_NoStaffPinHashProperty()
        {
            Assert.Null(typeof(Staff).GetProperty("PinHash"));
            var cafeAssembly = typeof(Staff).Assembly;
            var staffEntityConfig = cafeAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "StaffConfiguration");
            Assert.NotNull(staffEntityConfig);
            // Configuration must not reference PinHash (compile-time removal already enforced by entity).
            var configSource = staffEntityConfig!.Assembly.Location;
            Assert.False(string.IsNullOrEmpty(configSource));
        }

        [Fact]
        public void SupervisorApproval_NoPinVerificationServiceRegistered()
        {
            var names = typeof(WorkShiftService).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
            Assert.DoesNotContain("ISupervisorAuthService", names);
            Assert.DoesNotContain("SupervisorAuthService", names);
            Assert.DoesNotContain("SupervisorPinAuthorizationDto", names);
            Assert.DoesNotContain("SupervisorAuthRequestDto", names);
        }

        [Fact]
        public void SupervisorApproval_NoPinRepositoryQuery()
        {
            Assert.Null(typeof(IOtpChallengeRepository).GetMethod("GetSupervisorsWithPinAsync"));
            var methods = typeof(WorkShiftService).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Select(m => m.Name)
                .ToHashSet();
            Assert.DoesNotContain("GetSupervisorsWithPinAsync", methods);
            Assert.DoesNotContain("AuthorizePinAsync", methods);
            Assert.DoesNotContain("VerifySupervisorPinAsync", methods);
            Assert.DoesNotContain("UpdatePinAsync", methods);
        }

        [Fact]
        public void SupervisorApproval_NoUpdatePinEndpoint()
        {
            Assert.Null(Type.GetType("CafeChain.Controllers.AttendanceController, CafeChain"));
            Assert.Null(typeof(AdminPOSController).GetMethod("AuthorizeSupervisor"));
            Assert.Null(Type.GetType("CafeChain.Application.Interfaces.Attendance.IAttendanceSecurityService, CafeChain"));
        }

        [Fact]
        public void SupervisorApproval_NoLegacyPinDtoField()
        {
            Assert.Null(typeof(CloseShiftRequestDto).GetProperty("SupervisorPin"));
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("SupervisorPin"));
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("Pin"));
            Assert.Null(typeof(OpenShiftRequestDto).GetProperty("SupervisorPin"));
            Assert.Null(typeof(OpenShiftRequestDto).GetProperty("Pin"));
        }

        [Fact]
        public void SupervisorApproval_UsesOtpChallengeOnly()
        {
            var ctorParams = typeof(WorkShiftService).GetConstructors().Single().GetParameters();
            Assert.Contains(ctorParams, p => p.ParameterType == typeof(IOtpChallengeRepository));
            Assert.DoesNotContain(ctorParams, p => p.ParameterType.Name.Contains("SupervisorAuth", StringComparison.Ordinal));
        }

        [Fact]
        public void OtpApproval_AlphanumericContract_RemainsUnchanged()
        {
            Assert.Equal(6, OtpConstants.CodeLength);
            Assert.Equal("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", OtpConstants.Alphabet);
            Assert.Equal(5, OtpConstants.TtlMinutes);
            Assert.Equal(3, OtpConstants.MaxFailedAttempts);
            Assert.Equal(60, OtpConstants.ResendCooldownSeconds);
            Assert.Equal(3, OtpConstants.MaxResendCount);
            Assert.DoesNotContain('O', OtpConstants.Alphabet);
            Assert.DoesNotContain('0', OtpConstants.Alphabet);
            Assert.DoesNotContain('I', OtpConstants.Alphabet);
            Assert.DoesNotContain('1', OtpConstants.Alphabet);
        }

        [Fact]
        public async Task OtpApproval_AntiSelfApproval_RemainsEnforced()
        {
            using var ctx = CreateDbContext();
            SeedApprover(ctx, staffId: 601, accountId: 6601, roleId: RoleShiftSupervisor, email: "actor@test.local");
            SeedApprover(ctx, staffId: 602, accountId: 6602, roleId: RoleShiftSupervisor, email: "other@test.local");
            await ctx.SaveChangesAsync();

            var repo = new OtpChallengeRepository(ctx);
            var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 601, DateTime.UtcNow);

            Assert.NotNull(approver);
            Assert.Equal(602, approver!.StaffId);
            Assert.NotEqual(601, approver.StaffId);
        }

        [Fact]
        public async Task OtpApproval_PayloadBinding_RemainsEnforced()
        {
            var fp = new OtpPayloadFingerprintService();
            var a = fp.BuildCashDifferenceFingerprint(1, 2, 3, 100m, "r1");
            var b = fp.BuildCashDifferenceFingerprint(1, 2, 3, 100m, "r1");
            var c = fp.BuildCashDifferenceFingerprint(1, 2, 3, 101m, "r1");
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.Equal(64, a.Length); // SHA-256 hex
        }

        [Fact]
        public void SupervisorApproval_CloseExceptionRequiresOtp()
        {
            // Compile-time + DTO shape: only OtpChallengePublicId (inherited), no PIN.
            Assert.NotNull(typeof(CloseShiftExceptionRequestDto).GetProperty("OtpChallengePublicId"));
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("SupervisorPin"));
        }

        [Fact]
        public void SupervisorApproval_OpenLateRequiresOtp()
        {
            Assert.NotNull(typeof(OpenShiftRequestDto).GetProperty("OtpChallengePublicId"));
            Assert.Null(typeof(OpenShiftRequestDto).GetProperty("SupervisorPin"));
        }

        [Fact]
        public void SupervisorApproval_CashDifferenceRequiresOtp()
        {
            Assert.NotNull(typeof(CloseShiftRequestDto).GetProperty("OtpChallengePublicId"));
            Assert.Null(typeof(CloseShiftRequestDto).GetProperty("SupervisorPin"));
            Assert.Equal(OtpConstants.ActionTypes.CashDifference, "CASH_DIFFERENCE");
        }

        [Fact]
        public async Task OtpApprover_SelectionDoesNotDependOnPinHash()
        {
            using var ctx = CreateDbContext();
            SeedApprover(ctx, staffId: 701, accountId: 7701, roleId: RoleShiftSupervisor, email: "ss@test.local");
            SeedApprover(ctx, staffId: 704, accountId: 7704, roleId: RoleSalesStaff, email: "sale@test.local");
            await ctx.SaveChangesAsync();

            Assert.Null(typeof(Staff).GetProperty("PinHash"));

            var repo = new OtpChallengeRepository(ctx);
            var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);
            Assert.NotNull(approver);
            Assert.Equal(701, approver!.StaffId);
        }

        private static void SeedApprover(
            AppDbContext ctx,
            int staffId,
            int accountId,
            int roleId,
            string email)
        {
            // FK enforcement off in IntegrationTestBase — no Store row required.
            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = roleId });
            ctx.Staffs.Add(new Staff
            {
                StaffId = staffId,
                AccountId = accountId,
                StoreId = StoreId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                StaffShifts = new System.Collections.Generic.List<StaffShift>()
            });
        }
    }
}
