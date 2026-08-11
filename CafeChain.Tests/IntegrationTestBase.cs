using CafeChain.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using CafeChain.Models.Permissions;
using CafeChain.Models.Customers;
using CafeChain.Application.Constants;

namespace CafeChain.Tests
{
    /// <summary>
    /// AppDbContext variant cho SQLite tests.
    /// Override OnModelCreating để loại bỏ SQL Server-specific syntax mà SQLite không hỗ trợ:
    ///   - HasColumnType("nvarchar(max)") → SQLite chỉ cần TEXT
    ///   - HasDefaultValueSql("GETDATE()") → SQLite dùng datetime('now')
    ///   - HasFilter("[Col] IS NOT NULL") → SQLite hỗ trợ nhưng cú pháp hơi khác
    /// </summary>
    public class TestDbContext : AppDbContext
    {
        public TestDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Sau khi base áp dụng tất cả entity configurations,
            // duyệt qua và sửa các SQL Server-specific column types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var columnType = property.GetColumnType();
                    var defaultSql = property.GetDefaultValueSql();

                    // nvarchar(max) → TEXT (SQLite native)
                    if (columnType != null && columnType.Contains("max", StringComparison.OrdinalIgnoreCase))
                    {
                        property.SetColumnType("TEXT");
                    }

                    if (defaultSql?.Contains("SYSUTCDATETIME", StringComparison.OrdinalIgnoreCase) == true
                        || defaultSql?.Contains("GETUTCDATE", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        property.SetDefaultValueSql("datetime('now')");
                    }

                    // SQL Server [Timestamp]/IsRowVersion() tự generate + kiểm tra giá trị,
                    // SQLite không hỗ trợ → set default + tắt concurrency check
                    // Nếu không tắt: SaveChangesAsync fail do RowVersion stale (SQLite không auto-update)
                    if (property.IsConcurrencyToken && property.ClrType == typeof(byte[]))
                    {
                        property.SetDefaultValue(new byte[] { 0 });
                        property.IsConcurrencyToken = false;
                    }
                }

                // Xóa CHECK constraints không tương thích với SQLite hoặc cản ADR-0001
                // CK_StoreInventory_AvailableQty >= 0 → ADR-0001 cho phép âm kho (Blind Selling)
                // CK_StoreInventories_XOR_Item → cần linh hoạt trong test
                var checkConstraints = entityType.GetCheckConstraints().ToList();
                foreach (var constraint in checkConstraints)
                {
                    entityType.RemoveCheckConstraint(constraint.ModelName);
                }

                // #120 aggregation tests: allow multiple RecipeDetail lines that resolve to the
                // same StoreInventoryId (production still enforces unique Ingredient/Child pairs).
                if (entityType.ClrType.Name == "RecipeDetail")
                {
                    foreach (var index in entityType.GetIndexes().ToList())
                    {
                        var props = index.Properties.Select(p => p.Name).ToArray();
                        if (props.SequenceEqual(new[] { "RecipeId", "IngredientId" })
                            || props.SequenceEqual(new[] { "RecipeId", "ChildRecipeId" }))
                        {
                            index.IsUnique = false;
                        }
                    }
                }

                // OTP Phase 1: SQL Server filtered unique (one active challenge).
                // Drop uniqueness/filter on SQLite — app-layer enforces one-active there.
                if (entityType.ClrType.Name == "OtpChallenge")
                {
                    foreach (var index in entityType.GetIndexes().ToList())
                    {
                        if (index.Name == "UX_OtpChallenges_OneActivePerActorActionTarget"
                            || (index.IsUnique && !string.IsNullOrEmpty(index.GetFilter())))
                        {
                            index.IsUnique = false;
                            index.SetFilter(null);
                        }
                    }
                }


            }
        }
    }

    /// <summary>
    /// Base class cho Integration Tests — sử dụng SQLite In-Memory.
    /// 
    /// Tại sao SQLite thay vì EF Core InMemory?
    ///   - SQLite hỗ trợ Unique Index + Foreign Key constraints
    ///   - EF InMemory KHÔNG hỗ trợ Filtered Index — không thể test ADR-0002 (ClientOrderId)
    ///   - SQLite hỗ trợ Transactions — sát thực tế production hơn
    /// 
    /// Lifecycle:
    ///   1. Constructor: mở SQLite connection + tạo schema (EnsureCreated)
    ///   2. CreateDbContext(): tạo TestDbContext mới (SQLite-compatible)
    ///   3. Dispose(): đóng connection → xóa toàn bộ dữ liệu
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        private readonly SqliteConnection _connection;
        protected readonly DbContextOptions<AppDbContext> DbOptions;

        protected IntegrationTestBase()
        {
            // Mở connection TRƯỚC khi tạo options — SQLite in-memory database
            // chỉ tồn tại khi connection còn mở
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Disable FK enforcement — test ADR business logic, không test referential integrity
            // Tránh phải seed 30+ lookup tables chỉ để thỏa FK constraints
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = OFF;";
                cmd.ExecuteNonQuery();
            }

            DbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            // Tạo schema qua TestDbContext — loại bỏ SQL Server syntax incompatible
            using var context = CreateDbContext();
            context.Database.EnsureCreated();
            // Production defaults come from Scripts/SeedAll.sql. SQLite tests use
            // an explicit local fixture so they do not depend on EF HasData.
            context.Roles.AddRange(
                new Role { RoleId = 1, Name = RoleConstants.BusinessOwner, Active = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 2, Name = RoleConstants.AreaManager, Active = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 3, Name = RoleConstants.StoreManager, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 4, Name = RoleConstants.SalesStaff, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 5, Name = RoleConstants.AccountantWarehouse, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 6, Name = RoleConstants.SystemAdmin, Active = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Role { RoleId = 8, Name = RoleConstants.ShiftSupervisor, Active = true, IsStoreLevel = true, CreatedAt = new DateTime(2026, 1, 1) });
            context.AccountRoles.AddRange(
                new AccountRole { AccountId = 1, RoleId = 1 },
                new AccountRole { AccountId = 2, RoleId = 2 },
                new AccountRole { AccountId = 3, RoleId = 3 },
                new AccountRole { AccountId = 4, RoleId = 4 },
                new AccountRole { AccountId = 5, RoleId = 5 },
                new AccountRole { AccountId = 6, RoleId = 6 },
                new AccountRole { AccountId = 15, RoleId = 8 });
            context.SaveChanges();
        }

        /// <summary>
        /// Tạo TestDbContext — SQLite-compatible version của AppDbContext.
        /// Mỗi test nên dùng context riêng để tránh EF change tracker cache.
        /// </summary>
        protected AppDbContext CreateDbContext()
        {
            return new TestDbContext(DbOptions);
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
