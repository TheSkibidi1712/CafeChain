//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Threading.Tasks;
//using CafeChain.Application.DTOs.Admin.PreparedItems;
//using CafeChain.Application.Services.Admin.PreparedItems;
//using CafeChain.Application.Services.Admin.Recipes;
//using CafeChain.Application.Services.Inventories;
//using CafeChain.Areas.Admin.Controllers;
//using CafeChain.Data;
//using CafeChain.Models.Drinks;
//using CafeChain.Models.Enums.Unit;
//using CafeChain.Models.Inventories.Ingredients;
//using CafeChain.Models.Inventories.PreparedItems;
//using CafeChain.ViewModels.Admin.Recipes;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging.Abstractions;
//using Xunit;

//namespace CafeChain.Tests.POS
//{
//    /// <summary>Issue #126 — PreparedItem master + BOM builder UX contracts (additive, no domain redesign).</summary>
//    public class AdminUxPreparedItemBomIssue126Tests : IntegrationTestBase
//    {
//        private const int UnitG = 1;
//        private const int UnitKg = 2;
//        private const int UnitMl = 3;
//        private const int UnitL = 4;

//        private static void EnsureUnits(AppDbContext ctx)
//        {
//            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
//            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
//            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
//            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);
//        }

//        private static void EnsureUnit(AppDbContext ctx, int unitId, string code, UnitType type)
//        {
//            var u = ctx.Units.FirstOrDefault(x => x.UnitId == unitId);
//            if (u != null)
//            {
//                u.UnitCode = code;
//                u.Type = type;
//                u.Active = true;
//                u.Name = code;
//                ctx.SaveChanges();
//                return;
//            }

//            if (ctx.Units.Any(x => x.UnitCode.ToLower() == code.ToLower()))
//                return;

//            ctx.Units.Add(new Unit
//            {
//                UnitId = unitId,
//                UnitCode = code,
//                Name = code,
//                Type = type,
//                Active = true
//            });
//            ctx.SaveChanges();
//        }

//        private static AdminRecipeService CreateRecipeService(AppDbContext ctx)
//        {
//            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//            var normalizer = new RecipeOutputNormalizer(ctx, physical);
//            return new AdminRecipeService(ctx, normalizer);
//        }

//        private async Task<int> CreatePiAsync(AppDbContext ctx, string code, string name)
//        {
//            EnsureUnits(ctx);
//            var svc = new AdminPreparedItemService(ctx);
//            return await svc.CreateAsync(new AdminPreparedItemSaveDTO
//            {
//                Code = code,
//                Name = name,
//                BaseUnitId = UnitG
//            });
//        }

//        private static List<RecipeDetailVM> OneIng(int ingredientId)
//            => new()
//            {
//                new RecipeDetailVM
//                {
//                    ItemCode = $"ING_{ingredientId}",
//                    Quantity = 10m,
//                    UnitId = UnitG,
//                    YieldPercentage = 100
//                }
//            };

//        private int EnsureIngredient(AppDbContext ctx)
//        {
//            EnsureUnits(ctx);
//            var existing = ctx.Ingredients.FirstOrDefault(i => i.Code == "ING-TEST-126");
//            if (existing != null) return existing.IngredientId;
//            var ing = new Ingredient
//            {
//                Code = "ING-TEST-126",
//                Name = "Sữa test",
//                BaseUnitId = UnitG,
//                Active = true
//            };
//            ctx.Ingredients.Add(ing);
//            ctx.SaveChanges();
//            return ing.IngredientId;
//        }

//        [Fact]
//        public void AdminPreparedItem_EmptyState_ShowsCreateFirstCta()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminPreparedItem", "Index.cshtml");
//            Assert.True(File.Exists(path), path);
//            var html = File.ReadAllText(path);
//            Assert.Contains("Chưa có bán thành phẩm", html, StringComparison.Ordinal);
//            Assert.Contains("Tạo bán thành phẩm đầu tiên", html, StringComparison.Ordinal);
//            Assert.Contains("preparedItemEmptyState", html, StringComparison.Ordinal);
//            Assert.Contains("Danh mục bán thành phẩm", html, StringComparison.Ordinal);
//            Assert.DoesNotContain("Bán thành phẩm (kho)", html, StringComparison.Ordinal);
//        }

//        [Fact]
//        public async Task AdminPreparedItem_List_ShowsActiveRecipeAndVersionCount()
//        {
//            using var ctx = CreateDbContext();
//            EnsureUnits(ctx);
//            var ingId = EnsureIngredient(ctx);
//            var piId = await CreatePiAsync(ctx, "BTP-LIST-1", "Trân châu list");
//            var recipeSvc = CreateRecipeService(ctx);
//            var create = await recipeSvc.CreateRecipeAsync(new RecipeCreateVM
//            {
//                RecipeType = "SUBRECIPE",
//                PreparedItemId = piId,
//                ExpectedYield = 4.5m,
//                OutputUnitId = UnitKg,
//                Active = true,
//                EffectiveDate = DateTime.Today,
//                Details = OneIng(ingId)
//            });
//            Assert.True(create.IsSuccess, create.Message);

//            var listSvc = new AdminPreparedItemService(ctx);
//            var (items, total) = await listSvc.GetPagedAsync("BTP-LIST-1", true, 1, 20);
//            Assert.Equal(1, total);
//            var row = Assert.Single(items);
//            Assert.NotNull(row.ActiveRecipeId);
//            Assert.True(row.VersionCount >= 1);
//            Assert.Equal("has_active", row.ConfigStatusKey);
//            Assert.Equal("Có công thức hoạt động", row.ConfigStatus);
//        }

//        [Fact]
//        public async Task AdminPreparedItem_List_Query_DoesNotUsePerRowRecipeLookup()
//        {
//            // Structural: LoadRecipeStatsAsync is private batch method; verify multi-id single stats load path via reflection + count.
//            using var ctx = CreateDbContext();
//            EnsureUnits(ctx);
//            var svc = new AdminPreparedItemService(ctx);
//            var ids = new List<int>();
//            for (var i = 0; i < 3; i++)
//            {
//                ids.Add(await svc.CreateAsync(new AdminPreparedItemSaveDTO
//                {
//                    Code = $"BTP-BATCH-{i}",
//                    Name = $"Batch {i}",
//                    BaseUnitId = UnitG
//                }));
//            }

//            var method = typeof(AdminPreparedItemService).GetMethod(
//                "LoadRecipeStatsAsync",
//                BindingFlags.Instance | BindingFlags.NonPublic);
//            Assert.NotNull(method);
//            var task = (Task)method!.Invoke(svc, new object[] { ids })!;
//            await task.ConfigureAwait(false);
//            var resultProp = task.GetType().GetProperty("Result");
//            var dict = resultProp!.GetValue(task);
//            Assert.NotNull(dict);
//            Assert.Equal(3, ((System.Collections.IDictionary)dict!).Count);
//        }

//        [Fact]
//        public void AdminRecipe_Create_Btp_Combobox_SearchByCodeAndName()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
//            var js = Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js");
//            var html = File.ReadAllText(path);
//            var script = File.ReadAllText(js);
//            Assert.Contains("preparedItemSelect", html, StringComparison.Ordinal);
//            Assert.Contains("name=\"PreparedItemId\"", html, StringComparison.Ordinal);
//            Assert.Contains("matcher", script, StringComparison.Ordinal);
//            Assert.Contains("data-code", script, StringComparison.Ordinal);
//            Assert.Contains("data-name", script, StringComparison.Ordinal);
//            Assert.Contains("Làm mới danh sách", html, StringComparison.Ordinal);
//            Assert.Contains("Tạo bán thành phẩm", html, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_Create_Btp_BindsPreparedItemId_NotRecipeId()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
//            var html = File.ReadAllText(path);
//            Assert.Contains("name=\"PreparedItemId\"", html, StringComparison.Ordinal);
//            Assert.Contains("Không dùng RecipeId làm đầu ra", html, StringComparison.OrdinalIgnoreCase);
//            Assert.DoesNotContain("name=\"OutputRecipeId\"", html, StringComparison.Ordinal);
//        }

//        [Fact]
//        public async Task AdminRecipe_Create_WhenActiveExists_BlocksAndLinksToVersionEdit()
//        {
//            using var ctx = CreateDbContext();
//            EnsureUnits(ctx);
//            var ingId = EnsureIngredient(ctx);
//            var piId = await CreatePiAsync(ctx, "BTP-ACT-1", "Active conflict");
//            var svc = CreateRecipeService(ctx);
//            var first = await svc.CreateRecipeAsync(new RecipeCreateVM
//            {
//                RecipeType = "SUBRECIPE",
//                PreparedItemId = piId,
//                ExpectedYield = 1m,
//                OutputUnitId = UnitG,
//                Active = true,
//                EffectiveDate = DateTime.Today,
//                Details = OneIng(ingId)
//            });
//            Assert.True(first.IsSuccess, first.Message);

//            var second = await svc.CreateRecipeAsync(new RecipeCreateVM
//            {
//                RecipeType = "SUBRECIPE",
//                PreparedItemId = piId,
//                ExpectedYield = 2m,
//                OutputUnitId = UnitG,
//                Active = true,
//                EffectiveDate = DateTime.Today,
//                Details = OneIng(ingId)
//            });
//            Assert.False(second.IsSuccess);
//            Assert.Contains("hoạt động", second.Message, StringComparison.OrdinalIgnoreCase);

//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            Assert.Contains("Tạo phiên bản mới", js, StringComparison.Ordinal);
//            Assert.Contains("createBlockedByActiveRecipe", js, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_OutputLabel_ExpectedYield_MapsToOutputQuantity()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
//            var html = File.ReadAllText(path);
//            Assert.Contains("Sản lượng đầu ra của một mẻ", html, StringComparison.Ordinal);
//            // Field contract: ExpectedYield (name attribute after #129 typed page model)
//            Assert.Contains("name=\"ExpectedYield\"", html, StringComparison.Ordinal);
//            Assert.DoesNotContain("Sản lượng dự kiến sau hao hụt chuẩn", html, StringComparison.Ordinal);
//            Assert.Contains("Không nhân thêm Yield", html, StringComparison.OrdinalIgnoreCase);
//        }

//        [Fact]
//        public async Task AdminRecipe_NormalizedPreview_MatchesBackend()
//        {
//            using var ctx = CreateDbContext();
//            EnsureUnits(ctx);
//            var piId = await CreatePiAsync(ctx, "BTP-PREV", "Preview PI");
//            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//            var normalizer = new RecipeOutputNormalizer(ctx, physical);
//            var result = await normalizer.NormalizeAsync(piId, 4.5m, UnitKg);
//            Assert.True(result.IsSuccess, result.Message);
//            Assert.Equal(4500m, result.Data!.NormalizedQuantityInBase);
//            Assert.Equal("g", result.Data.BaseUnitCode, StringComparer.OrdinalIgnoreCase);
//        }

//        [Fact]
//        public void AdminRecipe_BomRow_ChildRecipe_PinsRecipeId()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
//            var html = File.ReadAllText(path);
//            Assert.Contains("REC_", html, StringComparison.Ordinal);
//            Assert.Contains("pin Recipe", html, StringComparison.OrdinalIgnoreCase);
//            Assert.Contains("Bán thành phẩm con", html, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_DuplicateItemCode_ClientWarn_ServerReject()
//        {
//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            Assert.Contains("findDuplicateRows", js, StringComparison.Ordinal);
//            Assert.Contains("btn-merge-dup", js, StringComparison.Ordinal);
//            Assert.Contains("hasDup", js, StringComparison.Ordinal);

//            // Server still rejects
//            using var ctx = CreateDbContext();
//            EnsureUnits(ctx);
//            var ingId = EnsureIngredient(ctx);
//            var svc = CreateRecipeService(ctx);
//            var details = new List<RecipeDetailVM>
//            {
//                new() { ItemCode = $"ING_{ingId}", Quantity = 1, UnitId = UnitG, YieldPercentage = 100 },
//                new() { ItemCode = $"ING_{ingId}", Quantity = 2, UnitId = UnitG, YieldPercentage = 100 }
//            };
//            var pi = new AdminPreparedItemService(ctx).CreateAsync(new AdminPreparedItemSaveDTO
//            {
//                Code = "BTP-DUP",
//                Name = "Dup",
//                BaseUnitId = UnitG
//            }).GetAwaiter().GetResult();
//            var result = svc.CreateRecipeAsync(new RecipeCreateVM
//            {
//                RecipeType = "SUBRECIPE",
//                PreparedItemId = pi,
//                ExpectedYield = 1,
//                OutputUnitId = UnitG,
//                Active = true,
//                EffectiveDate = DateTime.Today,
//                Details = details
//            }).GetAwaiter().GetResult();
//            Assert.False(result.IsSuccess);
//            Assert.Contains("Trùng", result.Message, StringComparison.OrdinalIgnoreCase);
//        }

//        [Fact]
//        public void AdminRecipe_DuplicateCompatibleRows_CanMergeClientSide()
//        {
//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            Assert.Contains("unitA !== unitB", js, StringComparison.Ordinal);
//            Assert.Contains("q1 + q2", js, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_CostIncomplete_ListsSpecificIssueCodes()
//        {
//            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            var html = File.ReadAllText(path);
//            Assert.Contains("displayCostIssues", html, StringComparison.Ordinal);
//            Assert.Contains("Không thể tính đầy đủ giá vốn", js, StringComparison.Ordinal);
//            Assert.Contains("costmessage", js, StringComparison.OrdinalIgnoreCase);
//            Assert.DoesNotContain("fake zero", js, StringComparison.OrdinalIgnoreCase);
//        }

//        [Fact]
//        public void AdminRecipe_List_TypeFromIdentityFields_NotNameHeuristic()
//        {
//            // #129: type key resolution lives in AdminRecipeQueryService (thin controller).
//            var queryPath = Path.Combine(FindRepoRoot(), "CafeChain", "Application", "Services", "Admin", "Recipes", "AdminRecipeQueryService.cs");
//            var src = File.ReadAllText(queryPath);
//            Assert.Contains("ResolveRecipeTypeKey", src, StringComparison.Ordinal);
//            Assert.Contains("ToppingId", src, StringComparison.Ordinal);
//            Assert.Contains("PreparedItemId", src, StringComparison.Ordinal);
//            Assert.DoesNotContain("Name.Contains(\"Lít\")", src, StringComparison.Ordinal);
//            Assert.DoesNotContain("Name.Contains(\"Kg\")", src, StringComparison.Ordinal);

//            var index = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Index.cshtml"));
//            Assert.DoesNotContain("Name.Contains", index, StringComparison.Ordinal);
//            Assert.Contains("data-recipe-type", index, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_List_ShowsOutputPerBatch_NotYieldPercent()
//        {
//            var index = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Index.cshtml"));
//            Assert.Contains("Sản lượng mỗi mẻ", index, StringComparison.Ordinal);
//            Assert.DoesNotContain("YieldPercentage", index, StringComparison.Ordinal);
//            Assert.Contains("Xem cấu trúc BOM", index, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_Create_PostPayload_PreservesFieldNames()
//        {
//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            Assert.Contains("RecipeType:", js, StringComparison.Ordinal);
//            Assert.Contains("PreparedItemId:", js, StringComparison.Ordinal);
//            Assert.Contains("ExpectedYield:", js, StringComparison.Ordinal);
//            Assert.Contains("OutputUnitId:", js, StringComparison.Ordinal);
//            Assert.Contains("YieldPercentage: 100", js, StringComparison.Ordinal);
//            Assert.Contains("ItemCode:", js, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_DestructiveActions_HaveAccessibleNames()
//        {
//            var create = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml"));
//            var index = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Index.cshtml"));
//            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
//            Assert.Contains("Xóa dòng", js, StringComparison.Ordinal);
//            Assert.Contains("aria-label", js, StringComparison.Ordinal);
//            Assert.Contains("Xóa", index, StringComparison.Ordinal);
//            Assert.DoesNotContain("fa-trash-alt", create, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminRecipe_Create_Edit_UseConsistentLightTheme()
//        {
//            var create = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml"));
//            var edit = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Edit.cshtml"));
//            Assert.Contains("data-theme=\"light\"", create, StringComparison.Ordinal);
//            Assert.Contains("data-theme=\"light\"", edit, StringComparison.Ordinal);
//            Assert.Contains("rb-page", create, StringComparison.Ordinal);
//            Assert.Contains("rb-page", edit, StringComparison.Ordinal);
//            Assert.DoesNotContain("linear-gradient(135deg, #1e293b", edit, StringComparison.Ordinal);
//        }

//        [Fact]
//        public void AdminNavigation_PreparedItemMaster_IsUnderProductionBom()
//        {
//            var layout = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml"));
//            var prodIdx = layout.IndexOf("Sản xuất / BOM", StringComparison.Ordinal);
//            var invIdx = layout.IndexOf("Kho & Cung ứng", StringComparison.Ordinal);
//            var masterIdx = layout.IndexOf("Danh mục bán thành phẩm", StringComparison.Ordinal);
//            var bomIdx = layout.IndexOf("Công thức BOM", StringComparison.Ordinal);
//            Assert.True(prodIdx > 0 && masterIdx > prodIdx && masterIdx < invIdx);
//            Assert.True(bomIdx > masterIdx);
//            Assert.DoesNotContain("Bán thành phẩm (kho)", layout, StringComparison.Ordinal);
//            Assert.Contains("asp-controller=\"AdminPreparedItem\"", layout, StringComparison.Ordinal);
//        }

//        private static string FindRepoRoot()
//        {
//            var dir = new DirectoryInfo(AppContext.BaseDirectory);
//            while (dir != null)
//            {
//                if (File.Exists(Path.Combine(dir.FullName, "CafeChain.slnx"))
//                    || File.Exists(Path.Combine(dir.FullName, "CafeChain", "CafeChain.csproj")))
//                {
//                    // tests run from CafeChain.Tests/bin → root may be solution folder containing CafeChain/
//                    if (Directory.Exists(Path.Combine(dir.FullName, "CafeChain", "Areas")))
//                        return dir.FullName;
//                    if (Directory.Exists(Path.Combine(dir.FullName, "Areas")))
//                        return dir.Parent?.FullName ?? dir.FullName;
//                }
//                dir = dir.Parent;
//            }
//            // Fallback: workspace relative from test assembly
//            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
//        }
//    }
//}
