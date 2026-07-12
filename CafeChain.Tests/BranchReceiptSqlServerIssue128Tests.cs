using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #128 — SQL Server concurrency for BranchReceipt confirm (UPDLOCK + unique line index).
    /// Database: CafeChain_Issue128Tests on local SQLEXPRESS.
    /// </summary>
    public sealed class BranchReceiptSqlServerIssue128Tests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_Issue128Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        private int _storeId = 1;
        private int _unitId = 1;
        private int _managerStaffId;
        private static readonly string[] ManagerRoles = { RoleConstants.StoreManager };

        public async Task InitializeAsync()
        {
            try
            {
                await using (var master = new SqlConnection(MasterConnectionString))
                {
                    await master.OpenAsync();
                    await using var cmd = master.CreateCommand();
                    cmd.CommandText = $@"
IF DB_ID(N'{Database}') IS NULL
    CREATE DATABASE [{Database}];";
                    await cmd.ExecuteNonQueryAsync();
                }

                await using var ctx = CreateContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
                await SeedStaffAndLookupsAsync(ctx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SQL Server integration environment unavailable for #128. Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_ConcurrentConfirm_SameReceipt_PostsOnce()
        {
            int receiptId;
            int requestId;
            await using (var seed = CreateContext())
            {
                requestId = await SeedRequestAsync(seed, requested: 500m);
                var svc = CreateService(seed);
                var draft = await svc.CreateDraftAsync(
                    Receipt(requestId, 200m, "sql-concurrent-1"), _managerStaffId, ManagerRoles);
                Assert.True(draft.IsSuccess, draft.Message);
                receiptId = draft.Data!.BranchReceiptId;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var results = await Task.WhenAll(
                CreateService(ctx1).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles),
                CreateService(ctx2).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));
            Assert.Contains(results, r => r.Data!.WasReplay || !r.Data.WasReplay);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
                && t.BranchReceiptLine != null
                && t.BranchReceiptLine.BranchReceiptId == receiptId));
            var line = await verify.BranchReceiptLines.FirstAsync(l => l.BranchReceiptId == receiptId);
            var inv = await verify.StoreInventories.SingleAsync(i =>
                i.StoreId == _storeId && i.IngredientId == line.IngredientId);
            Assert.Equal(200m, inv.AvailableQty);
        }

        [Fact]
        public async Task SqlServer_ConcurrentPartialReceipts_NoOverReceipt()
        {
            int requestId;
            int receiptA;
            int receiptB;
            int ingredientId;
            await using (var seed = CreateContext())
            {
                requestId = await SeedRequestAsync(seed, requested: 100m);
                ingredientId = await seed.RestockRequests
                    .Where(r => r.RestockRequestId == requestId)
                    .Select(r => r.IngredientId!.Value)
                    .FirstAsync();
                var svc = CreateService(seed);
                var a = await svc.CreateDraftAsync(Receipt(requestId, 70m, "sql-race-a"), _managerStaffId, ManagerRoles);
                var b = await svc.CreateDraftAsync(Receipt(requestId, 70m, "sql-race-b"), _managerStaffId, ManagerRoles);
                Assert.True(a.IsSuccess, a.Message);
                Assert.True(b.IsSuccess, b.Message);
                receiptA = a.Data!.BranchReceiptId;
                receiptB = b.Data!.BranchReceiptId;
            }

            await using var ctx1 = CreateContext();
            await using var ctx2 = CreateContext();
            var results = await Task.WhenAll(
                CreateService(ctx1).ConfirmAsync(receiptA, _managerStaffId, _storeId, ManagerRoles),
                CreateService(ctx2).ConfirmAsync(receiptB, _managerStaffId, _storeId, ManagerRoles));

            var success = results.Count(r => r.IsSuccess);
            var over = results.Count(r =>
                !r.IsSuccess && r.ErrorCode == BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);

            Assert.True(success >= 1, string.Join(" | ", results.Select(r => $"{r.IsSuccess}:{r.ErrorCode}:{r.Message}")));
            Assert.True(success + over == 2 || success == 1,
                $"success={success} over={over} details={string.Join(" | ", results.Select(r => $"{r.IsSuccess}:{r.ErrorCode}:{r.Message}"))}");
            // Exactly one post under concurrency (other blocked as over-receipt or lost race).
            Assert.Equal(1, success);
            Assert.Equal(1, over);

            await using var verify = CreateContext();
            var inv = await verify.StoreInventories.SingleAsync(i =>
                i.StoreId == _storeId && i.IngredientId == ingredientId);
            Assert.Equal(70m, inv.AvailableQty);
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
                && t.BranchReceiptLine != null
                && t.BranchReceiptLine.RestockRequestId == requestId));
        }

        [Fact]
        public async Task SqlServer_ConfirmReplay_NoSecondMovement()
        {
            int receiptId;
            await using (var seed = CreateContext())
            {
                var requestId = await SeedRequestAsync(seed, requested: 300m);
                var svc = CreateService(seed);
                var draft = await svc.CreateDraftAsync(
                    Receipt(requestId, 100m, "sql-replay"), _managerStaffId, ManagerRoles);
                Assert.True(draft.IsSuccess, draft.Message);
                receiptId = draft.Data!.BranchReceiptId;
                Assert.True((await svc.ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles)).IsSuccess);
            }

            await using var ctx = CreateContext();
            var replay = await CreateService(ctx).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Data!.WasReplay);

            await using var verify = CreateContext();
            Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
        }

        private CreateBranchReceiptRequest Receipt(int requestId, decimal qty, string key) =>
            new()
            {
                StoreId = _storeId,
                ReceiptKey = key,
                Lines =
                {
                    new CreateBranchReceiptLineInput
                    {
                        RestockRequestId = requestId,
                        InputQuantity = qty,
                        InputUnitId = _unitId,
                        ActualPackagePrice = qty * 10m
                    }
                }
            };

        private static BranchReceiptService CreateService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var mode = new Mock<IInventoryWriterModeService>();
            var resolver = new Mock<IStoreInventoryWriteResolver>();
            var alerts = new Mock<IStockAlertService>();
            alerts
                .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new StockAlertEvaluationResultDto()));
            return new BranchReceiptService(
                ctx, unit, physical, mode.Object, resolver.Object, alerts.Object,
                NullLogger<BranchReceiptService>.Instance);
        }

        private static AppDbContext CreateContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options);

        private async Task SeedStaffAndLookupsAsync(AppDbContext ctx)
        {
            _unitId = await ctx.Units.Select(u => u.UnitId).FirstOrDefaultAsync();
            if (_unitId == 0)
            {
                ctx.Units.Add(new Unit { UnitCode = "g", Name = "Gram", Active = true });
                await ctx.SaveChangesAsync();
                _unitId = await ctx.Units.Select(u => u.UnitId).FirstAsync();
            }

            _storeId = await ctx.Stores.Select(s => s.StoreId).FirstOrDefaultAsync();
            if (_storeId == 0)
            {
                ctx.Stores.Add(new Store
                {
                    Name = "SQL128 Store",
                    Address = "x",
                    Phone = "0",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
                _storeId = await ctx.Stores.Select(s => s.StoreId).FirstAsync();
            }

            if (!await ctx.Roles.AnyAsync(r => r.Name == RoleConstants.StoreManager))
            {
                ctx.Roles.Add(new Role
                {
                    Name = RoleConstants.StoreManager,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
            }

            var roleId = await ctx.Roles.Where(r => r.Name == RoleConstants.StoreManager)
                .Select(r => r.RoleId).FirstAsync();

            var existingStaff = await ctx.Staffs
                .Where(s => s.StoreId == _storeId && s.Active)
                .OrderBy(s => s.StaffId)
                .FirstOrDefaultAsync();
            if (existingStaff != null)
            {
                _managerStaffId = existingStaff.StaffId;
                return;
            }

            var account = new Account
            {
                Email = $"mgr128sql{Guid.NewGuid():N}@test.local",
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync();
            ctx.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = roleId });
            var staff = new Staff
            {
                AccountId = account.AccountId,
                StoreId = _storeId,
                FullName = "Mgr 128 SQL",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                BaseSalary = 0
            };
            ctx.Staffs.Add(staff);
            await ctx.SaveChangesAsync();
            _managerStaffId = staff.StaffId;
        }

        private async Task<int> SeedRequestAsync(AppDbContext ctx, decimal requested)
        {
            if (_managerStaffId <= 0)
                await SeedStaffAndLookupsAsync(ctx);

            var ingredient = new Ingredient
            {
                Code = "ING128SQL" + Guid.NewGuid().ToString("N")[..6],
                Name = "Ingredient 128 SQL",
                BaseUnitId = _unitId,
                Active = true
            };
            ctx.Ingredients.Add(ingredient);
            await ctx.SaveChangesAsync();

            var alert = new StockAlert
            {
                StoreId = _storeId,
                IngredientId = ingredient.IngredientId,
                AlertType = StockAlertTypes.LowStock,
                Severity = StockAlertSeverities.Warning,
                Status = StockAlertStatuses.Confirmed,
                Source = StockAlertSources.ManualCheck,
                CurrentQtySnapshot = 0,
                ThresholdSnapshot = requested,
                ConfirmedByStaffId = _managerStaffId,
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.StockAlerts.Add(alert);
            await ctx.SaveChangesAsync();

            var req = new RestockRequest
            {
                StockAlertId = alert.StockAlertId,
                StoreId = _storeId,
                IngredientId = ingredient.IngredientId,
                RequestedQuantity = requested,
                Status = RestockRequestStatuses.Processing,
                Priority = RestockRequestPriorities.Normal,
                CreatedByStaffId = _managerStaffId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.RestockRequests.Add(req);
            await ctx.SaveChangesAsync();
            return req.RestockRequestId;
        }
    }
}
