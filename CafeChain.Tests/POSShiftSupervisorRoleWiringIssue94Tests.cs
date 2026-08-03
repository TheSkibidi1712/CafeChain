// using System.Collections.Generic;
// using System.Linq;
// using System.Security.Claims;
// using System.Threading.Tasks;
// using CafeChain.Application.Constants;
// using CafeChain.Application.Interfaces.Attendance;
// using CafeChain.Application.Interfaces.Security;
// using CafeChain.Application.Services.Admin.Staffs;
// using CafeChain.Controllers;
// using CafeChain.Infrastructure.Repositories.Admin.POS;
// using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
// using CafeChain.Models.Customers;
// using CafeChain.Models.Permissions;
// using CafeChain.Models.Staffs;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Configuration;
// using Moq;
// using Xunit;

// namespace CafeChain.Tests.POS
// {
//     /// <summary>
//     /// Issue #94 — Wire Role ShiftSupervisor ("Ca trưởng") for AdminStaff, StaffHub/POS, OTP approver selection.
//     /// </summary>
//     public class POSShiftSupervisorRoleWiringIssue94Tests : IntegrationTestBase
//     {
//         private const int StoreId = 50;
//         private const int RoleStoreManager = 3;
//         private const int RoleSalesStaff = 4;
//         private const int RoleAccountant = 5;
//         private const int RoleShiftSupervisor = 8;

//         // -----------------------------------------------------------------
//         // AdminStaff assignment allow-lists
//         // -----------------------------------------------------------------

//         [Fact]
//         public async Task AdminStaff_AdminDropdown_IncludesShiftSupervisor_ExcludesCustomer()
//         {
//             var service = CreateAdminStaffService(out _);
//             var data = await service.GetMasterDataForFormAsync(Principal(RoleConstants.BusinessOwner));

//             Assert.Contains(data.Roles, r => r.RoleId == RoleShiftSupervisor && r.Name == RoleConstants.ShiftSupervisor);
//             Assert.DoesNotContain(data.Roles, r => r.RoleId == 7 || r.Name == RoleConstants.Customer);
//         }

//         [Fact]
//         public async Task AdminStaff_StoreManagerDropdown_IncludesShiftSupervisor_Sales_Accountant()
//         {
//             var service = CreateAdminStaffService(out _);
//             var data = await service.GetMasterDataForFormAsync(
//                 Principal(RoleConstants.StoreManager, staffId: 1, storeId: StoreId));

//             var ids = data.Roles.Select(r => r.RoleId).OrderBy(x => x).ToList();
//             Assert.Equal(new[] { RoleSalesStaff, RoleAccountant, RoleShiftSupervisor }.OrderBy(x => x), ids);
//             Assert.DoesNotContain(data.Roles, r => r.RoleId == RoleStoreManager);
//         }

//         [Fact]
//         public async Task AdminStaff_AreaManagerDropdown_IncludesStoreManager_Sales_ShiftSupervisor_Accountant()
//         {
//             var service = CreateAdminStaffService(out _);
//             var data = await service.GetMasterDataForFormAsync(
//                 Principal(RoleConstants.AreaManager, staffId: 2, storeId: StoreId));

//             var ids = data.Roles.Select(r => r.RoleId).OrderBy(x => x).ToList();
//             Assert.Equal(
//                 new[] { RoleStoreManager, RoleSalesStaff, RoleAccountant, RoleShiftSupervisor }.OrderBy(x => x),
//                 ids);
//             Assert.DoesNotContain(data.Roles, r => r.RoleId == 1); // BusinessOwner
//             Assert.DoesNotContain(data.Roles, r => r.RoleId == 6); // SystemAdmin
//         }

//         // -----------------------------------------------------------------
//         // StaffHub / POS access
//         // -----------------------------------------------------------------

//         [Theory]
//         [InlineData(RoleConstants.SalesStaff, true)]
//         [InlineData(RoleConstants.ShiftSupervisor, true)]
//         [InlineData(RoleConstants.AccountantWarehouse, false)]
//         [InlineData(RoleConstants.StoreManager, false)]
//         public void StaffHub_IssuePosToken_AllowsSalesAndShiftSupervisor_Only(string role, bool allowed)
//         {
//             var controller = CreateStaffHubController(role, staffId: "10", storeId: StoreId.ToString());

//             var result = controller.IssuePosToken();

//             if (allowed)
//             {
//                 var ok = Assert.IsType<OkObjectResult>(result);
//                 Assert.NotNull(ok.Value);
//             }
//             else
//             {
//                 var status = Assert.IsType<ObjectResult>(result);
//                 Assert.Equal(403, status.StatusCode);
//             }
//         }

//         // -----------------------------------------------------------------
//         // OTP approver priority
//         // -----------------------------------------------------------------

//         [Fact]
//         public async Task OtpApprover_PrefersShiftSupervisor_OverStoreManager_AndAccountant()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 101, accountId: 1001, roleId: RoleStoreManager, email: "sm@test.local");
//             SeedApproverStaff(ctx, staffId: 102, accountId: 1002, roleId: RoleShiftSupervisor, email: "ss@test.local");
//             SeedApproverStaff(ctx, staffId: 103, accountId: 1003, roleId: RoleAccountant, email: "acc@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             // Phase 1: exclude actor (0 = no self) — still prefers ShiftSupervisor over SM/AW.
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             // Email/OTP must target Ca trưởng assigned at store (DB), not StoreManager first.
//             Assert.Equal(102, approver!.StaffId);
//             Assert.Equal("ss@test.local", approver.Account!.Email);
//         }

//         [Fact]
//         public async Task OtpApprover_UsesShiftSupervisorEmail_FromDatabaseAccount()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             const string liveEmail = "catruong.store50@cafechain.vn";
//             SeedApproverStaff(ctx, staffId: 210, accountId: 2210, roleId: RoleShiftSupervisor, email: liveEmail);
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(210, approver!.StaffId);
//             Assert.Equal(liveEmail, approver.Account!.Email);
//         }

//         [Fact]
//         public async Task OtpApprover_SelectsShiftSupervisor_WhenNoStoreManager_ButAccountantExists()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 202, accountId: 2002, roleId: RoleShiftSupervisor, email: "ss@test.local");
//             SeedApproverStaff(ctx, staffId: 203, accountId: 2003, roleId: RoleAccountant, email: "acc@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(202, approver!.StaffId);
//         }

//         [Fact]
//         public async Task OtpApprover_FallsBackToStoreManager_WhenNoShiftSupervisor()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 301, accountId: 3001, roleId: RoleStoreManager, email: "sm-only@test.local");
//             SeedApproverStaff(ctx, staffId: 303, accountId: 3003, roleId: RoleAccountant, email: "acc@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(301, approver!.StaffId);
//             Assert.Equal("sm-only@test.local", approver.Account!.Email);
//         }

//         [Fact]
//         public async Task OtpApprover_DoesNotSelectAccountant_WhenOnlyAccountantExists_Phase1()
//         {
//             // Phase 1 anti-self-approval hardening: no AccountantWarehouse default for OTP.
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 303, accountId: 3003, roleId: RoleAccountant, email: "acc@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.Null(approver);
//         }

//         [Fact]
//         public async Task OtpApprover_ExcludesSalesStaff()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 404, accountId: 4004, roleId: RoleSalesStaff, email: "sale@test.local");
//             SeedApproverStaff(ctx, staffId: 402, accountId: 4002, roleId: RoleShiftSupervisor, email: "ss@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(402, approver!.StaffId);
//             Assert.NotEqual(404, approver.StaffId);
//         }

//         [Fact]
//         public async Task OtpApprover_ExcludesActor_AntiSelfApproval()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 501, accountId: 5001, roleId: RoleShiftSupervisor, email: "actor-ss@test.local");
//             SeedApproverStaff(ctx, staffId: 502, accountId: 5002, roleId: RoleStoreManager, email: "sm@test.local");
//             await ctx.SaveChangesAsync();

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 501, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(502, approver!.StaffId);
//             Assert.NotEqual(501, approver.StaffId);
//         }

//         // -----------------------------------------------------------------
//         // OTP approver eligibility (no Staff.PinHash)
//         // -----------------------------------------------------------------

//         [Fact]
//         public async Task OtpApprover_IncludesShiftSupervisor_WithoutPinHash_ExcludeSalesStaff()
//         {
//             using var ctx = CreateDbContext();
//             SeedRoles(ctx);
//             SeedApproverStaff(ctx, staffId: 502, accountId: 5002, roleId: RoleShiftSupervisor, email: "ss@test.local");
//             SeedApproverStaff(ctx, staffId: 504, accountId: 5004, roleId: RoleSalesStaff, email: "sale@test.local");
//             SeedApproverStaff(ctx, staffId: 503, accountId: 5003, roleId: RoleAccountant, email: "acc@test.local");
//             await ctx.SaveChangesAsync();

//             Assert.Null(typeof(Staff).GetProperty("PinHash"));

//             var repo = new OtpChallengeRepository(ctx);
//             var approver = await repo.GetOtpApproverAsync(StoreId, excludeStaffId: 0, DateTime.UtcNow);

//             Assert.NotNull(approver);
//             Assert.Equal(502, approver!.StaffId);
//             Assert.NotEqual(504, approver.StaffId);
//             Assert.NotEqual(503, approver.StaffId);
//         }

//         // -----------------------------------------------------------------
//         // Helpers
//         // -----------------------------------------------------------------

//         private static AdminStaffService CreateAdminStaffService(out Mock<IAdminStaffRepository> repo)
//         {
//             repo = new Mock<IAdminStaffRepository>(MockBehavior.Strict);
//             var allRoles = new List<Role>
//             {
//                 Role(1, RoleConstants.BusinessOwner, false),
//                 Role(2, RoleConstants.AreaManager, false),
//                 Role(3, RoleConstants.StoreManager, true),
//                 Role(4, RoleConstants.SalesStaff, true),
//                 Role(5, RoleConstants.AccountantWarehouse, true),
//                 Role(6, RoleConstants.SystemAdmin, false),
//                 Role(7, RoleConstants.Customer, false),
//                 Role(8, RoleConstants.ShiftSupervisor, true),
//             };

//             repo.Setup(r => r.GetRolesForDropdownAsync(null)).ReturnsAsync(allRoles);
//             repo.Setup(r => r.GetScopeTypesAsync()).ReturnsAsync(new List<ScopeType>());
//             repo.Setup(r => r.GetStoreByIdAsync(StoreId)).ReturnsAsync(new CafeChain.Models.Stores.Store
//             {
//                 StoreId = StoreId,
//                 Name = "Test Store",
//                 Active = true
//             });
//             repo.Setup(r => r.GetActiveStoresAsync()).ReturnsAsync(new List<CafeChain.Models.Stores.Store>());

//             var scopeAuth = new Mock<IScopeAuthorizationService>(MockBehavior.Loose);
//             scopeAuth
//                 .Setup(s => s.GetAllowedStoresAsync(It.IsAny<int>()))
//                 .ReturnsAsync(new List<CafeChain.Models.Stores.Store>());

//             var env = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
//             return new AdminStaffService(repo.Object, env.Object, scopeAuth.Object);
//         }

//         private static Role Role(int id, string name, bool storeLevel) => new()
//         {
//             RoleId = id,
//             Name = name,
//             Active = true,
//             IsStoreLevel = storeLevel,
//             CreatedAt = DateTime.UtcNow
//         };

//         private static ClaimsPrincipal Principal(string role, int staffId = 1, int storeId = StoreId)
//         {
//             var identity = new ClaimsIdentity(new[]
//             {
//                 new Claim(ClaimTypes.Role, role),
//                 new Claim("StaffId", staffId.ToString()),
//                 new Claim("StoreId", storeId.ToString()),
//             }, authenticationType: "Test");
//             return new ClaimsPrincipal(identity);
//         }

//         private static StaffHubController CreateStaffHubController(string role, string staffId, string storeId)
//         {
//             var action = new Mock<IAttendanceActionService>(MockBehavior.Loose);
//             var security = new Mock<IAttendanceSecurityService>(MockBehavior.Loose);
//             var config = new ConfigurationBuilder()
//                 .AddInMemoryCollection(new Dictionary<string, string?>
//                 {
//                     ["Jwt:Key"] = "unit-test-signing-key-32-characters-minimum",
//                     ["Jwt:Issuer"] = "CafeChain",
//                     ["Jwt:Audience"] = "CafeChain.POS",
//                     ["Jwt:ExpirationHours"] = "12",
//                     ["PosFrontend:Url"] = "http://localhost:5173/order"
//                 })
//                 .Build();

//             var controller = new StaffHubController(action.Object, security.Object, config);
//             var claims = new List<Claim>
//             {
//                 new(ClaimTypes.NameIdentifier, "1"),
//                 new(ClaimTypes.Name, "tester"),
//                 new(ClaimTypes.Email, "t@test.local"),
//                 new(ClaimTypes.Role, role),
//                 new("StaffId", staffId),
//                 new("StoreId", storeId),
//             };
//             controller.ControllerContext = new ControllerContext
//             {
//                 HttpContext = new DefaultHttpContext
//                 {
//                     User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
//                 }
//             };
//             return controller;
//         }

//         private static void SeedRoles(CafeChain.Data.AppDbContext ctx)
//         {
//             // EnsureCreated already seeds Roles via RoleConfiguration (incl. RoleId 8).
//             // No-op if present; only used as documentation of expected ids.
//             _ = ctx.Roles.Count();
//         }

//         private static void SeedApproverStaff(
//             CafeChain.Data.AppDbContext ctx,
//             int staffId,
//             int accountId,
//             int roleId,
//             string email)
//         {
//             ctx.Accounts.Add(new Account
//             {
//                 AccountId = accountId,
//                 Email = email,
//                 PasswordHash = "x",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow
//             });

//             ctx.AccountRoles.Add(new AccountRole
//             {
//                 AccountId = accountId,
//                 RoleId = roleId
//             });

//             ctx.Staffs.Add(new Staff
//             {
//                 StaffId = staffId,
//                 AccountId = accountId,
//                 StoreId = StoreId,
//                 FullName = $"Staff {staffId}",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow,
//                 BaseSalary = 0,
//                 StaffShifts = new List<StaffShift>()
//             });
//         }
//     }
// }
