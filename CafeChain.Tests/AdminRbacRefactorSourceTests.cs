// using System.Text.RegularExpressions;

// namespace CafeChain.Tests;

// public sealed class AdminRbacRefactorSourceTests
// {
//     [Fact]
//     public void Initial_baseline_removes_staff_payroll_tables_and_adds_structured_address()
//     {
//         var migration = ReadInitialMigration();
//         var staffSection = migration[migration.IndexOf("name: \"Staffs\"", StringComparison.Ordinal)..
//             migration.IndexOf("name: \"StaffAddresses\"", StringComparison.Ordinal)];

//         foreach (var removed in new[] { "SalaryType", "BaseSalary", "Allowance", "ProbationRate", "OvertimeRate", "SocialInsuranceNumber", "HealthInsuranceNumber" })
//             Assert.DoesNotContain(removed, staffSection, StringComparison.Ordinal);
//         Assert.DoesNotContain("name: \"StaffBanks\"", migration, StringComparison.Ordinal);
//         Assert.DoesNotContain("name: \"StaffDependents\"", migration, StringComparison.Ordinal);
//         Assert.Contains("FK_StaffAddresses_Provinces_ProvinceId", migration, StringComparison.Ordinal);
//         Assert.Contains("FK_StaffAddresses_Districts_DistrictId", migration, StringComparison.Ordinal);
//         Assert.Contains("FK_StaffAddresses_Wards_WardId", migration, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Seed_catalog_is_insert_only_and_reserves_permission_100()
//     {
//         var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
//         var start = seed.IndexOf("BATCH 12B - ACTIVE ADMIN PERMISSION CATALOG", StringComparison.Ordinal);
//         Assert.True(start >= 0);
//         var catalog = seed[start..];

//         Assert.Contains("20260721173046_InitialCreate", seed, StringComparison.Ordinal);
//         Assert.Contains("(28,", catalog, StringComparison.Ordinal);
//         Assert.Contains("PermissionId=100", catalog, StringComparison.Ordinal);
//         Assert.Contains("24 groups / 125 permissions / 345 role grants", catalog, StringComparison.Ordinal);
//         Assert.Contains("Shift.Cancel", catalog, StringComparison.Ordinal);
//         Assert.DoesNotContain("Shift.Publish", catalog, StringComparison.Ordinal);
//         Assert.DoesNotContain("StoreIP.", catalog, StringComparison.OrdinalIgnoreCase);
//         Assert.DoesNotMatch(new Regex(@"\b(?:UPDATE|MERGE)\s+(?:dbo\.)?(?:Permissions|PermissionGroups|RolePermissions)\b",
//             RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), catalog);
//         Assert.Contains("WHERE NOT EXISTS", catalog, StringComparison.OrdinalIgnoreCase);
//     }

//     [Fact]
//     public void Shift_v13_has_schedule_only_schema_permissions_and_no_attendance_module()
//     {
//         var migration = ReadInitialMigration();
//         var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminStaffShiftController.cs");
//         var service = Read("CafeChain", "Application", "Services", "Admin", "Staffs", "AdminStaffShiftService.cs");
//         var staffHub = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");

//         foreach (var removed in new[] { "AttendanceLogs", "StoreIPs", "FaceDescriptor", "IsFreeShift", "IsAdHoc", "ActualCheckIn", "ActualCheckOut", "PayrollHours", "CHECKED_IN", "ABSENT" })
//             Assert.DoesNotContain(removed, migration, StringComparison.Ordinal);
//         Assert.Contains("SCHEDULED", migration, StringComparison.Ordinal);
//         Assert.Contains("CANCELLED", migration, StringComparison.Ordinal);
//         Assert.Contains("PermissionConstants.ShiftCancel", controller, StringComparison.Ordinal);
//         Assert.Contains("GetPotentialOverlapsAsync", service, StringComparison.Ordinal);
//         Assert.Contains("date.AddDays(-1)", service, StringComparison.Ordinal);
//         Assert.DoesNotContain("Face ID", staffHub, StringComparison.OrdinalIgnoreCase);
//         Assert.DoesNotContain("check-in", staffHub, StringComparison.OrdinalIgnoreCase);
//     }

//     [Fact]
//     public void Staff_store_and_ingredient_controllers_use_permission_policies_and_no_dbcontext()
//     {
//         foreach (var controllerName in new[] { "AdminStaffController.cs", "AdminStoreController.cs", "AdminIngredientController.cs" })
//         {
//             var source = Read("CafeChain", "Areas", "Admin", "Controllers", controllerName);
//             Assert.Contains("RequirePermission", source, StringComparison.Ordinal);
//             Assert.DoesNotContain("AppDbContext", source, StringComparison.Ordinal);
//         }

//         var staff = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminStaffController.cs");
//         Assert.Contains("RequirePermission(PermissionConstants.StaffCreate)", staff, StringComparison.Ordinal);
//         Assert.DoesNotContain("Authorize(Roles", staff, StringComparison.Ordinal);
//         Assert.DoesNotContain("[AllowAnonymous]", staff, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Permission_ui_searches_accentless_group_and_code_and_does_not_stretch_cards()
//     {
//         var script = Read("CafeChain", "wwwroot", "js", "Admin", "Permissions", "admin-permissions.js");
//         var css = Read("CafeChain", "wwwroot", "css", "Admin", "Permissions", "admin-permissions.css");

//         Assert.Contains("normalize(\"NFD\")", script, StringComparison.Ordinal);
//         Assert.Contains("permissionGroupId", script, StringComparison.Ordinal);
//         Assert.Contains("data-group-text", script, StringComparison.Ordinal);
//         Assert.Contains("data-permission-text", script, StringComparison.Ordinal);
//         Assert.Contains("canChange", script, StringComparison.OrdinalIgnoreCase);
//         Assert.Contains("align-items: start", css, StringComparison.Ordinal);
//         Assert.Contains("align-self: start", css, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Store_location_button_reverse_geocodes_and_cascades_all_location_levels()
//     {
//         foreach (var viewName in new[] { "Create.cshtml", "Edit.cshtml" })
//         {
//             var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminStore", viewName);
//             Assert.Contains("navigator.geolocation", view, StringComparison.Ordinal);
//             Assert.Contains("performReverseGeocoding", view, StringComparison.Ordinal);
//             Assert.Contains("apiLoadDistricts", view, StringComparison.Ordinal);
//             Assert.Contains("apiLoadWards", view, StringComparison.Ordinal);
//             Assert.Contains("aria-busy", view, StringComparison.Ordinal);
//         }
//     }

//     [Fact]
//     public void Ingredient_commands_do_not_replace_conversions_or_toggle_active()
//     {
//         var createDto = Read("CafeChain", "Application", "DTOs", "Admin", "Ingredients", "AdminIngredientCreateDTO.cs");
//         var updateDto = Read("CafeChain", "Application", "DTOs", "Admin", "Ingredients", "AdminIngredientUpdateDTO.cs");
//         var service = Read("CafeChain", "Application", "Services", "Admin", "Ingredients", "AdminIngredientService.cs");

//         Assert.DoesNotContain("Conversions", createDto + updateDto, StringComparison.Ordinal);
//         Assert.DoesNotContain("public bool Active", updateDto, StringComparison.Ordinal);
//         Assert.Contains("HasBaseUnitDependenciesAsync", service, StringComparison.Ordinal);
//         Assert.DoesNotContain("ReplaceConversions", service, StringComparison.Ordinal);
//     }

//     [Fact]
//     public void Shared_mutation_guard_protects_admin_and_all_logout_layouts()
//     {
//         var guard = Read("CafeChain", "wwwroot", "js", "shared", "mutation-guard.js");
//         Assert.Contains("data-submit-busy", guard, StringComparison.Ordinal);
//         Assert.Contains("AdminMutationGuard", guard, StringComparison.Ordinal);
//         Assert.Contains("pageshow", guard, StringComparison.Ordinal);

//         foreach (var path in new[]
//         {
//             new[] { "CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml" },
//             new[] { "CafeChain", "Views", "Shared", "_Layout.cshtml" },
//             new[] { "CafeChain", "Views", "AppLauncher", "Index.cshtml" }
//         })
//         {
//             Assert.Contains("mutation-guard.js", Read(path), StringComparison.Ordinal);
//         }
//     }

//     [Fact]
//     public void Staff_create_form_uses_role_scope_store_order_and_vietnamese_scope_contract()
//     {
//         var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminStaff", "_CreateStaffModal.cshtml");
//         var mapping = Read("CafeChain", "Application", "Constants", "ScopeTypeDisplayNames.cs");
//         var document = Read("CafeChain", "Doc", "STAFF_SCOPE_MANAGEMENT_GUIDE.md");

//         var roleIndex = view.IndexOf("id=\"createRole\"", StringComparison.Ordinal);
//         var typeIndex = view.IndexOf("id=\"createScopeType\"", StringComparison.Ordinal);
//         var referenceIndex = view.IndexOf("id=\"createScopeRef\"", StringComparison.Ordinal);
//         var storeIndex = view.IndexOf("id=\"createStore\"", StringComparison.Ordinal);

//         Assert.True(roleIndex >= 0 && roleIndex < typeIndex);
//         Assert.True(typeIndex < referenceIndex);
//         Assert.True(referenceIndex < storeIndex);
//         Assert.Contains("data-scope-mode", view, StringComparison.Ordinal);
//         Assert.Contains("filterPrimaryStores", view, StringComparison.Ordinal);
//         Assert.Contains("GetDistricts", view, StringComparison.Ordinal);
//         Assert.Contains("GetWards", view, StringComparison.Ordinal);

//         foreach (var label in new[] { "Toàn chuỗi", "Tỉnh/Thành phố", "Quận/Huyện", "Phường/Xã", "Cửa hàng" })
//         {
//             Assert.Contains(label, mapping, StringComparison.Ordinal);
//             Assert.Contains(label, document, StringComparison.Ordinal);
//         }

//         Assert.Contains("Role", document, StringComparison.Ordinal);
//         Assert.Contains("StaffScope", document, StringComparison.Ordinal);
//         Assert.Contains("Staff.StoreId", document, StringComparison.Ordinal);
//     }

//     private static string Read(params string[] path) =>
//         File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

//     private static string ReadInitialMigration()
//     {
//         var files = Directory.GetFiles(Path.Combine(FindRepoRoot(), "CafeChain", "Migrations"), "*_InitialCreate.cs")
//             .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
//             .ToArray();
//         Assert.Single(files);
//         return File.ReadAllText(files[0]);
//     }

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
