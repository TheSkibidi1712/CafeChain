using System;

namespace CafeChain.Tests
{
    /// <summary>
    /// Shared SQL Server connection authority for disposable integration tests.
    /// Prefer env: CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING
    /// Template may use {Database} placeholder, or omit Database= (will be appended).
    /// Does not use operational CafeChain DB; each test suite supplies a dedicated name.
    /// </summary>
    public static class SqlServerTestConnection
    {
        public const string EnvVarName = "CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING";

        /// <summary>
        /// Default local instance when env var is unset (Windows Trusted Connection, no credentials).
        /// </summary>
        public const string DefaultServerTemplate =
            "Server=localhost\\SQLEXPRESS;Database={Database};Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true";

        public static string Create(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("databaseName is required.", nameof(databaseName));

            var template = Environment.GetEnvironmentVariable(EnvVarName);
            if (string.IsNullOrWhiteSpace(template))
                template = DefaultServerTemplate;

            template = template.Trim();

            if (template.Contains("{Database}", StringComparison.OrdinalIgnoreCase))
                return template.Replace("{Database}", databaseName, StringComparison.OrdinalIgnoreCase);

            // If caller provided a full connection string with a Database= segment, replace it.
            if (template.Contains("Database=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = template.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var i = 0; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                        parts[i] = "Database=" + databaseName;
                }
                return string.Join(";", parts);
            }

            return template.TrimEnd(';') + ";Database=" + databaseName + ";";
        }

        public static string MasterConnectionString()
        {
            var cs = Create("master");
            // Ensure we hit master even if template already set Database=
            if (cs.Contains("Database=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var i = 0; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                        parts[i] = "Database=master";
                }
                return string.Join(";", parts);
            }

            return cs.TrimEnd(';') + ";Database=master;";
        }
    }
}
