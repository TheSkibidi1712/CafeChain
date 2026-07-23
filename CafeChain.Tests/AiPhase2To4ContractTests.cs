// using System.Text.Json;

// namespace CafeChain.Tests;

// public sealed class AiPhase2To4ContractTests
// {
//     [Fact]
//     public void Dashboard_intelligence_exposes_only_the_eight_whitelisted_widgets()
//     {
//         var dto = Read("CafeChain", "Application", "DTOs", "Admin", "Dashboard", "DashboardIntelligenceDtos.cs");
//         var analyticsDto = Read("CafeChain", "Application", "DTOs", "Admin", "Dashboard", "DashboardAnalyticsDtos.cs");
//         var service = Read("CafeChain", "Application", "Services", "Admin", "Dashboard", "DashboardIntelligenceService.cs");

//         foreach (var widget in new[]
//         {
//             "NetSalesTrend", "StoreRanking", "TopProducts", "HourlyOrders",
//             "InventoryWasteByStoreIngredient", "OverduePurchaseOrders", "SupplierQuality", "WorkforceShiftStatus"
//         })
//         {
//             Assert.Contains(widget, analyticsDto, StringComparison.Ordinal);
//         }

//         Assert.Contains("UNSUPPORTED_INTENT", service, StringComparison.Ordinal);
//         Assert.Contains("_dashboard.GetPageAsync(actor", service, StringComparison.Ordinal);
//         Assert.Contains("StaffId = actor.StaffId", service, StringComparison.Ordinal);
//         Assert.DoesNotContain("Sql", dto, StringComparison.OrdinalIgnoreCase);
//         Assert.DoesNotContain("Procedure", dto, StringComparison.OrdinalIgnoreCase);
//         Assert.DoesNotContain("Column", dto, StringComparison.OrdinalIgnoreCase);
//     }

//     [Fact]
//     public void Forecast_runner_is_deterministic_and_uses_time_ordered_baselines()
//     {
//         var runner = Read("CafeChain", "Application", "Services", "AI", "ForecastModelRunner.cs");

//         Assert.Contains("SeasonalNaive", runner, StringComparison.Ordinal);
//         Assert.Contains("MovingAverage7", runner, StringComparison.Ordinal);
//         Assert.Contains("ExponentialSmoothing", runner, StringComparison.Ordinal);
//         Assert.Contains("HoltWintersAdditive", runner, StringComparison.Ordinal);
//         Assert.Contains("foldSize", runner, StringComparison.Ordinal);
//         Assert.Contains("values[..cut]", runner, StringComparison.Ordinal);
//         Assert.DoesNotContain("Random", runner, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Shift_proposal_apply_reloads_and_revalidates_before_one_transaction()
//     {
//         var service = Read("CafeChain", "Application", "Services", "Admin", "Staffs", "ShiftOptimizationService.cs");
//         var apply = service[service.IndexOf("public async Task ApplyAsync", StringComparison.Ordinal)..
//             service.IndexOf("public async Task SaveAvailabilityAsync", StringComparison.Ordinal)];

//         Assert.Contains("GetAvailabilityAsync", apply, StringComparison.Ordinal);
//         Assert.Contains("GetTimeOffsAsync", apply, StringComparison.Ordinal);
//         Assert.Contains("GetConstraintsAsync", apply, StringComparison.Ordinal);
//         Assert.Contains("GetRequirementsAsync", apply, StringComparison.Ordinal);
//         Assert.Contains("Eligible", apply, StringComparison.Ordinal);
//         Assert.Contains("BeginTransactionAsync", apply, StringComparison.Ordinal);
//         Assert.Equal(1, Count(apply, "SaveChangesAsync"));
//         Assert.DoesNotContain("AssignAsync", apply, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Pos_recommendations_are_bounded_optional_and_do_not_mutate_the_cart()
//     {
//         var service = Read("CafeChain", "Application", "Services", "AI", "PosRecommendationService.cs");
//         var repository = Read("CafeChain", "Infrastructure", "Repositories", "Analytics", "Phase4IntelligenceRepository.cs");
//         var client = Read("CafeChain", "wwwroot", "js", "pos-app.js");

//         Assert.Contains("Take(_options.MaximumResults)", service, StringComparison.Ordinal);
//         Assert.Contains("\"MaximumResults\": 3", Read("CafeChain", "appsettings.json"), StringComparison.Ordinal);
//         Assert.Contains("CONTROL", service, StringComparison.Ordinal);
//         Assert.Contains("RecommendationSessionId", service, StringComparison.Ordinal);
//         Assert.Contains("EvaluateStoreAsync", service, StringComparison.Ordinal);
//         Assert.Contains("state.IsSellable", service, StringComparison.Ordinal);
//         Assert.Contains("Refund", repository, StringComparison.OrdinalIgnoreCase);
//         Assert.Contains("recommendationSessionId", client, StringComparison.Ordinal);
//         Assert.Contains("add", client, StringComparison.OrdinalIgnoreCase);
//         Assert.DoesNotContain("autoAdd", client, StringComparison.OrdinalIgnoreCase);
//     }

//     [Fact]
//     public void Anomaly_detection_is_rule_first_scoped_and_never_claims_fraud()
//     {
//         var service = Read("CafeChain", "Application", "Services", "AI", "AnomalyDetectionService.cs");
//         var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalAnomaliesController.cs");

//         Assert.Contains("Median", service, StringComparison.Ordinal);
//         Assert.Contains("minimumAbsolute", service, StringComparison.Ordinal);
//         Assert.Contains("MinimumPercentageDeviation", service, StringComparison.Ordinal);
//         Assert.Contains("EnsureScope", service, StringComparison.Ordinal);
//         Assert.Contains("HIGH", service, StringComparison.Ordinal);
//         Assert.Contains("CRITICAL", service, StringComparison.Ordinal);
//         Assert.Contains("GetExplanationContextAsync", controller, StringComparison.Ordinal);
//         Assert.Contains("không phải kết luận gian lận", service, StringComparison.OrdinalIgnoreCase);
//     }

//     [Fact]
//     public void Every_phase_2_to_4_skill_schema_rejects_unknown_root_fields()
//     {
//         foreach (var schemaName in new[]
//         {
//             "dashboard-intent.schema.json", "dashboard-insight-explanation.schema.json",
//             "forecast-result-explanation.schema.json", "supplier-score-explanation.schema.json",
//             "shift-proposal-explanation.schema.json", "anomaly-explanation.schema.json"
//         })
//         {
//             using var document = JsonDocument.Parse(Read("CafeChain", "Resources", "AI", "schemas", schemaName));
//             Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
//         }
//     }

//     [Fact]
//     public void Phase_2_to_4_feature_flags_are_off_by_default()
//     {
//         using var document = JsonDocument.Parse(Read("CafeChain", "appsettings.json"));
//         var root = document.RootElement;

//         Assert.False(root.GetProperty("DashboardIntelligence").GetProperty("IntentParserEnabled").GetBoolean());
//         Assert.False(root.GetProperty("DashboardIntelligence").GetProperty("ExplanationEnabled").GetBoolean());
//         Assert.False(root.GetProperty("Forecasting").GetProperty("RevenueEnabled").GetBoolean());
//         Assert.False(root.GetProperty("Forecasting").GetProperty("ProductEnabled").GetBoolean());
//         Assert.False(root.GetProperty("SupplierIntelligence").GetProperty("ScoringEnabled").GetBoolean());
//         Assert.False(root.GetProperty("ShiftOptimization").GetProperty("ProposalEnabled").GetBoolean());
//         Assert.False(root.GetProperty("PosRecommendation").GetProperty("Enabled").GetBoolean());
//         Assert.False(root.GetProperty("AnomalyDetection").GetProperty("Enabled").GetBoolean());
//     }

//     [Fact]
//     public void Phase_2_to_4_migration_does_not_repeat_phase_1_columns()
//     {
//         var migration = Read("CafeChain", "Migrations", "20260722173421_AddAiIntelligencePhase2To4V3.cs");

//         Assert.Contains("ForecastRuns", migration, StringComparison.Ordinal);
//         Assert.Contains("ScheduleOptimizationProposals", migration, StringComparison.Ordinal);
//         Assert.Contains("PosRecommendationCatalog", migration, StringComparison.Ordinal);
//         Assert.Contains("OperationalAnomalies", migration, StringComparison.Ordinal);
//         Assert.DoesNotContain("AllowNegativeStock", migration, StringComparison.Ordinal);
//         Assert.DoesNotContain("DeduplicationKey", migration, StringComparison.Ordinal);
//     }

//     private static int Count(string value, string fragment) =>
//         (value.Length - value.Replace(fragment, string.Empty, StringComparison.Ordinal).Length) / fragment.Length;

//     private static string Read(params string[] path) =>
//         File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

//     private static string FindRepoRoot()
//     {
//         var directory = new DirectoryInfo(AppContext.BaseDirectory);
//         while (directory != null)
//         {
//             if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
//                 && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
//                 return directory.FullName;
//             directory = directory.Parent;
//         }
//         return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
//     }
// }
