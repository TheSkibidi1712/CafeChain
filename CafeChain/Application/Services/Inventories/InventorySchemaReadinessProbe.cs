using CafeChain.Application.DTOs.Inventories.Cutover;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #124 — fail-closed read-only probe of live DB schema objects required by #119–#123.
    /// Does not apply migrations or call Database.Migrate().
    /// </summary>
    public sealed class InventorySchemaReadinessProbe : IInventorySchemaReadinessProbe
    {
        private static readonly (string Table, string[] Columns)[] Required =
        {
            ("ProductionRuns", new[] { "ProductionRunId", "CompletedAt", "CompletedByStaffId", "RequestKey", "Status" }),
            ("InventoryTransactions", new[] { "ProductionRunId", "SourceRecipeId", "InventoryConsolidationRunId", "Type", "StoreInventoryId" }),
            ("StockAlerts", new[] { "PreparedItemId", "RecipeId", "Status" }),
            ("RestockRequests", new[] { "PreparedItemId", "RecipeId" }),
            ("InventoryConsolidationRuns", new[] { "InventoryConsolidationRunId", "StoreId", "RequestKey", "Status", "QueryContractVersion" }),
            ("InventoryConsolidationLines", new[] { "InventoryConsolidationLineId", "InventoryConsolidationRunId" }),
            ("StoreInventories", new[] { "PreparedItemId", "BtpIdentityState", "QuantitySemanticsStatus", "SupersededByStoreInventoryId" }),
            ("StoreInventoryWriterConfigurations", new[] { "WriterMode", "HasEverActivatedPreparedItem" }),
            ("InventoryWriterModeTransitions", new[] { "TransitionId", "ReadinessSnapshotJson", "ReadinessHash" }),
        };

        private static readonly string[] RequiredIndexHints =
        {
            "UX_InventoryConsolidationRuns_Store_RequestKey",
            "UX_InventoryTransactions_ConsolidationRun_Inventory_Type",
            "UX_InventoryTransactions_ProductionRun_Inventory_Type",
            "UX_StockAlert_Open_Store_PreparedItem",
            "IX_InventoryTransactions_SourceRecipeId",
            "IX_InventoryTransactions_ProductionRunId",
            "IX_InventoryTransactions_InventoryConsolidationRunId",
        };

        private readonly AppDbContext _context;

        public InventorySchemaReadinessProbe(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InventorySchemaReadinessReport> ProbeAsync(CancellationToken cancellationToken = default)
        {
            var missingTables = new List<string>();
            var missingColumns = new List<string>();
            var missingIndexes = new List<string>();
            var diagnostics = new List<string>();
            string? failureCode = null;

            try
            {
                if (!await _context.Database.CanConnectAsync(cancellationToken))
                {
                    return Fail("Cannot connect to database.", diagnostics);
                }

                var isSqlServer = _context.Database.IsSqlServer();
                var q = isSqlServer ? (Func<string, string>)(t => $"[{t}]") : (t => $"\"{t}\"");

                foreach (var (table, columns) in Required)
                {
                    if (!await TableExistsAsync(table, q, cancellationToken))
                    {
                        missingTables.Add(table);
                        continue;
                    }

                    foreach (var column in columns)
                    {
                        if (!await ColumnExistsAsync(table, column, isSqlServer, cancellationToken))
                            missingColumns.Add($"{table}.{column}");
                    }
                }

                // Indexes: soft check by exact name (EnsureCreated / regenerated InitialCreate may rename).
                // Tables + columns remain hard fail-closed; missing named indexes become diagnostics.
                foreach (var index in RequiredIndexHints)
                {
                    var exists = await IndexExistsAsync(index, isSqlServer, cancellationToken);
                    if (!exists)
                    {
                        missingIndexes.Add(index);
                        diagnostics.Add($"IndexSoftMissing:{index}");
                    }
                }

                diagnostics.Add(isSqlServer ? "Provider=SqlServer" : "Provider=Other");
            }
            catch (Exception ex)
            {
                return Fail("ProbeException=" + ex.GetType().Name + ":" + ex.Message, diagnostics);
            }

            // Hard gate: tables + columns. Named indexes soft-reported so schema contract remains valid
            // after teammate consolidated migrations with equivalent uniqueness under different names.
            var ready = missingTables.Count == 0 && missingColumns.Count == 0;

            if (!ready)
                failureCode = CutoverFailureCodes.SchemaContractNotReady;

            return new InventorySchemaReadinessReport
            {
                IsReady = ready,
                ContractVersion = CutoverContractVersions.Schema,
                ContractHash = ComputeContractHash(ready, missingTables, missingColumns, missingIndexes),
                CheckedAtUtc = DateTime.UtcNow,
                MissingTables = missingTables.OrderBy(x => x).ToList(),
                MissingColumns = missingColumns.OrderBy(x => x).ToList(),
                MissingIndexes = missingIndexes.OrderBy(x => x).ToList(),
                MissingForeignKeys = Array.Empty<string>(),
                MissingOrIncorrectChecks = Array.Empty<string>(),
                Diagnostics = diagnostics,
                FailureCode = failureCode
            };
        }

        private InventorySchemaReadinessReport Fail(string diagnostic, List<string> diagnostics)
        {
            diagnostics.Add(diagnostic);
            return new InventorySchemaReadinessReport
            {
                IsReady = false,
                ContractVersion = CutoverContractVersions.Schema,
                ContractHash = ComputeContractHash(false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
                CheckedAtUtc = DateTime.UtcNow,
                MissingTables = Array.Empty<string>(),
                MissingColumns = Array.Empty<string>(),
                MissingIndexes = Array.Empty<string>(),
                MissingForeignKeys = Array.Empty<string>(),
                MissingOrIncorrectChecks = Array.Empty<string>(),
                Diagnostics = diagnostics,
                FailureCode = CutoverFailureCodes.SchemaProbeFailed
            };
        }

        /// <summary>
        /// SQL Server: dedicated connection so metadata is not enlisted in activation SERIALIZABLE locks.
        /// SQLite in-memory: must reuse the same open connection (new connection = empty DB).
        /// </summary>
        private async Task<(System.Data.Common.DbConnection Conn, bool Owns)> OpenProbeConnectionAsync(CancellationToken ct)
        {
            if (_context.Database.IsSqlServer())
            {
                var source = _context.Database.GetDbConnection();
                var cs = _context.Database.GetConnectionString()
                         ?? source.ConnectionString
                         ?? throw new InvalidOperationException("No connection string for schema probe.");
                var conn = (System.Data.Common.DbConnection)Activator.CreateInstance(source.GetType())!;
                conn.ConnectionString = cs;
                await conn.OpenAsync(ct);
                return (conn, true);
            }

            var shared = _context.Database.GetDbConnection();
            if (shared.State != ConnectionState.Open)
                await shared.OpenAsync(ct);
            return (shared, false);
        }

        private async Task<bool> TableExistsAsync(string table, Func<string, string> q, CancellationToken ct)
        {
            try
            {
                var (conn, owns) = await OpenProbeConnectionAsync(ct);
                try
                {
                    await using var cmd = conn.CreateCommand();
                    // Do not enlist SQL Server independent connection in ambient TX.
                    cmd.CommandText = $"SELECT 1 AS x FROM {q(table)} WHERE 1=0";
                    await cmd.ExecuteScalarAsync(ct);
                    return true;
                }
                finally
                {
                    if (owns)
                        await conn.DisposeAsync();
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ColumnExistsAsync(string table, string column, bool sqlServer, CancellationToken ct)
        {
            try
            {
                var (conn, owns) = await OpenProbeConnectionAsync(ct);
                try
                {
                    await using var cmd = conn.CreateCommand();
                    if (sqlServer)
                    {
                        cmd.CommandText =
                            "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c";
                        var pt = cmd.CreateParameter();
                        pt.ParameterName = "@t";
                        pt.Value = table;
                        cmd.Parameters.Add(pt);
                        var pc = cmd.CreateParameter();
                        pc.ParameterName = "@c";
                        pc.Value = column;
                        cmd.Parameters.Add(pc);
                        var result = await cmd.ExecuteScalarAsync(ct);
                        return result != null && result != DBNull.Value;
                    }

                    cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }

                    return false;
                }
                finally
                {
                    if (owns)
                        await conn.DisposeAsync();
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IndexExistsAsync(string indexName, bool sqlServer, CancellationToken ct)
        {
            try
            {
                var (conn, owns) = await OpenProbeConnectionAsync(ct);
                try
                {
                    await using var cmd = conn.CreateCommand();
                    if (sqlServer)
                    {
                        cmd.CommandText = "SELECT 1 FROM sys.indexes WHERE name = @n";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@n";
                        p.Value = indexName;
                        cmd.Parameters.Add(p);
                    }
                    else
                    {
                        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name = @n";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@n";
                        p.Value = indexName;
                        cmd.Parameters.Add(p);
                    }

                    var result = await cmd.ExecuteScalarAsync(ct);
                    return result != null && result != DBNull.Value;
                }
                finally
                {
                    if (owns)
                        await conn.DisposeAsync();
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeContractHash(
            bool ready,
            IReadOnlyList<string> tables,
            IReadOnlyList<string> columns,
            IReadOnlyList<string> indexes)
        {
            var payload = JsonSerializer.Serialize(new
            {
                v = CutoverContractVersions.Schema,
                ready,
                tables = tables.OrderBy(x => x).ToArray(),
                columns = columns.OrderBy(x => x).ToArray(),
                indexes = indexes.OrderBy(x => x).ToArray()
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }
    }
}
