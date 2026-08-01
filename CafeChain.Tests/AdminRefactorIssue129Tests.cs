using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Services.Admin.Actor;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.ViewModels.Admin.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #129 — Admin boundary refactor characterization / architecture guards.</summary>
    public class AdminRefactorIssue129Tests : IntegrationTestBase
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CafeChain.slnx"))
                    || File.Exists(Path.Combine(dir.FullName, "CafeChain", "CafeChain.csproj")))
                    return dir.FullName.EndsWith("CafeChain", StringComparison.OrdinalIgnoreCase)
                           && File.Exists(Path.Combine(dir.FullName, "CafeChain.csproj"))
                        ? dir.Parent!.FullName
                        : dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static void EnsureUnit(AppDbContext ctx, int unitId, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == unitId);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                u.Name = code;
                ctx.SaveChanges();
                return;
            }
            ctx.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = code,
                Name = code,
                Type = type,
                Active = true
            });
            ctx.SaveChanges();
        }

        private static void EnsureUnits(AppDbContext ctx)
        {
            EnsureUnit(ctx, 1, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, 3, "ml", UnitType.TheTich);
        }

        [Fact]
        public void AdminRecipeController_DelegatesListToQueryService()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminRecipeController.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("IAdminRecipeQueryService", src, StringComparison.Ordinal);
            Assert.Contains("GetIndexPageAsync", src, StringComparison.Ordinal);
            Assert.DoesNotContain("_context.Recipes", src, StringComparison.Ordinal);
            Assert.DoesNotContain("BuildTreeHtml", src, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminRecipeController_DelegatesTreeBuildingToQueryService()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminRecipeController.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("IRecipeBomTreeQueryService", src, StringComparison.Ordinal);
            Assert.Contains("BuildTreeAsync", src, StringComparison.Ordinal);
            Assert.Contains("Partials/_BomTree", src, StringComparison.Ordinal);
            Assert.DoesNotContain("private async Task<string> BuildTreeHtml", src, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RecipeBomTreeQuery_UsesBoundedQueryShape()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);

            var root = new Recipe
            {
                Name = "Root129",
                RecipeCode = "R-129-ROOT",
                Status = "Active",
                Active = true,
                EffectiveDate = DateTime.Today
            };
            ctx.Recipes.Add(root);
            await ctx.SaveChangesAsync();

            var child = new Recipe
            {
                Name = "Child129",
                RecipeCode = "R-129-CHILD",
                Status = "Active",
                Active = true,
                EffectiveDate = DateTime.Today
            };
            ctx.Recipes.Add(child);
            await ctx.SaveChangesAsync();

            var ing = new Ingredient
            {
                Code = "ING129",
                Name = "Sugar129",
                BaseUnitId = 1,
                Active = true
            };
            ctx.Ingredients.Add(ing);
            await ctx.SaveChangesAsync();

            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = root.RecipeId,
                ChildRecipeId = child.RecipeId,
                Quantity = 1,
                UnitId = 1
            });
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = child.RecipeId,
                IngredientId = ing.IngredientId,
                Quantity = 10,
                UnitId = 1
            });
            await ctx.SaveChangesAsync();

            var svc = new RecipeBomTreeQueryService(ctx);
            var tree = await svc.BuildTreeAsync(root.RecipeId);

            Assert.False(tree.RootNotFound);
            Assert.Single(tree.Roots);
            Assert.Equal(RecipeBomTreeNodeKind.ChildRecipe, tree.Roots[0].Kind);
            Assert.Equal(child.RecipeId, tree.Roots[0].ChildRecipeId);
            Assert.Single(tree.Roots[0].Children);
            Assert.Equal(RecipeBomTreeNodeKind.Ingredient, tree.Roots[0].Children[0].Kind);
            Assert.Equal(ing.IngredientId, tree.Roots[0].Children[0].IngredientId);
        }

        [Fact]
        public async Task RecipeBomTreeQuery_PreservesPinnedRecipeVersion()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);

            var pinned = new Recipe
            {
                Name = "PinnedOld",
                RecipeCode = "PIN-OLD",
                Status = "Active",
                Active = true,
                EffectiveDate = DateTime.Today
            };
            var latest = new Recipe
            {
                Name = "LatestNew",
                RecipeCode = "PIN-NEW",
                Status = "Active",
                Active = true,
                EffectiveDate = DateTime.Today
            };
            var root = new Recipe
            {
                Name = "Parent",
                RecipeCode = "PIN-ROOT",
                Status = "Active",
                Active = true,
                EffectiveDate = DateTime.Today
            };
            ctx.Recipes.AddRange(pinned, latest, root);
            await ctx.SaveChangesAsync();

            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = root.RecipeId,
                ChildRecipeId = pinned.RecipeId,
                Quantity = 2,
                UnitId = 1
            });
            await ctx.SaveChangesAsync();

            var tree = await new RecipeBomTreeQueryService(ctx).BuildTreeAsync(root.RecipeId);
            var child = Assert.Single(tree.Roots);
            Assert.Equal(pinned.RecipeId, child.ChildRecipeId);
            Assert.Contains("PinnedOld", child.DisplayName, StringComparison.Ordinal);
            Assert.DoesNotContain("LatestNew", child.DisplayName, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RecipeBomTreeQuery_DetectsCycleWithoutInfiniteRecursion()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);

            var a = new Recipe { Name = "A", RecipeCode = "CY-A", Status = "Active", Active = true, EffectiveDate = DateTime.Today };
            var b = new Recipe { Name = "B", RecipeCode = "CY-B", Status = "Active", Active = true, EffectiveDate = DateTime.Today };
            ctx.Recipes.AddRange(a, b);
            await ctx.SaveChangesAsync();

            ctx.RecipeDetails.Add(new RecipeDetail { RecipeId = a.RecipeId, ChildRecipeId = b.RecipeId, Quantity = 1, UnitId = 1 });
            ctx.RecipeDetails.Add(new RecipeDetail { RecipeId = b.RecipeId, ChildRecipeId = a.RecipeId, Quantity = 1, UnitId = 1 });
            await ctx.SaveChangesAsync();

            var tree = await new RecipeBomTreeQueryService(ctx).BuildTreeAsync(a.RecipeId);
            Assert.False(tree.RootNotFound);
            var path = Flatten(tree.Roots);
            Assert.Contains(path, n => n.Kind == RecipeBomTreeNodeKind.CycleDetected);
            Assert.True(path.Count < 20);
        }

        private static List<RecipeBomTreeNodeVM> Flatten(IEnumerable<RecipeBomTreeNodeVM> nodes)
        {
            var list = new List<RecipeBomTreeNodeVM>();
            foreach (var n in nodes)
            {
                list.Add(n);
                if (n.Children != null && n.Children.Count > 0)
                    list.AddRange(Flatten(n.Children));
            }
            return list;
        }

        [Fact]
        public void AdminRecipe_CreateEdit_PostContractsRemainStable()
        {
            var create = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml");
            var html = File.ReadAllText(create);
            Assert.Contains("name=\"RecipeType\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"PreparedItemId\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"ExpectedYield\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"OutputUnitId\"", html, StringComparison.Ordinal);
            // Details rows are JS-generated with those names
            var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Recipe", "bom-builder.js"));
            Assert.Contains("Details[", js, StringComparison.Ordinal);
            Assert.Contains("ItemCode", js, StringComparison.Ordinal);
            Assert.Contains("YieldPercentage", js, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminRecipe_FormPageModel_HasTypedOptions()
        {
            var t = typeof(AdminRecipeFormPageVM);
            Assert.NotNull(t.GetProperty(nameof(AdminRecipeFormPageVM.Form)));
            Assert.NotNull(t.GetProperty(nameof(AdminRecipeFormPageVM.Options)));
            Assert.Equal(typeof(AdminRecipeFormOptionsVM), t.GetProperty(nameof(AdminRecipeFormPageVM.Options))!.PropertyType);
        }

        [Fact]
        public void AdminRecipe_ViewModels_DoNotRequireDynamicViewBag()
        {
            var create = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminRecipe", "Create.cshtml"));
            Assert.Contains("AdminRecipeFormPageVM", create, StringComparison.Ordinal);
            Assert.DoesNotContain("foreach (dynamic", create, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewBag.Ingredients", create, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewBag.Drinks", create, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminPreparedItem_ListProjection_RemainsBatchLoaded()
        {
            var method = typeof(AdminPreparedItemService).GetMethod(
                "LoadRecipeStatsAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
        }

        [Fact]
        public void AdminUnitConversion_Controller_DelegatesConflictEvaluation()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminUnitConversionController.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("IAdminUnitConversionService", src, StringComparison.Ordinal);
            Assert.Contains("EvaluateAsync", src, StringComparison.Ordinal);
            Assert.DoesNotContain("AppDbContext", src, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminUnitConversion_PostContract_RemainsStable()
        {
            var create = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminUnitConversion", "Create.cshtml"));
            Assert.Contains("name=\"IngredientId\"", create, StringComparison.Ordinal);
            Assert.Contains("name=\"FromUnitId\"", create, StringComparison.Ordinal);
            Assert.Contains("name=\"FromQuantity\"", create, StringComparison.Ordinal);
            Assert.Contains("name=\"ToUnitId\"", create, StringComparison.Ordinal);
            Assert.Contains("name=\"ToQuantity\"", create, StringComparison.Ordinal);
            Assert.Contains("name=\"PackageConflictAcknowledged\"", create, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminRestockRequest_Controller_DoesNotContainStateTransitionLogic()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminRestockRequestsController.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("IRestockRequestWorkflowService", src, StringComparison.Ordinal);
            Assert.Contains("StartProcessingAsync", src, StringComparison.Ordinal);
            Assert.DoesNotContain("BeginTransaction", src, StringComparison.Ordinal);
            Assert.DoesNotContain("StoreInventories", src, StringComparison.Ordinal);
            Assert.DoesNotContain("InventoryTransactions", src, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminBranchReceipt_Controller_DoesNotMutateInventory()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminBranchReceiptsController.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("IBranchReceiptService", src, StringComparison.Ordinal);
            Assert.Contains("ConfirmAsync", src, StringComparison.Ordinal);
            Assert.DoesNotContain("BeginTransaction", src, StringComparison.Ordinal);
            Assert.DoesNotContain("StoreInventories", src, StringComparison.Ordinal);
            Assert.DoesNotContain("InventoryCostLayers", src, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminBranchReceipt_ConfirmTransactionContract_RemainsUnchanged()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Application", "Services", "Inventories", "BranchReceiptService.cs");
            var src = File.ReadAllText(path);
            Assert.Contains("ConfirmAsync", src, StringComparison.Ordinal);
            Assert.Contains("BeginTransactionAsync", src, StringComparison.Ordinal);
            Assert.Contains("BRANCH_RECEIPT_IN", src, StringComparison.Ordinal);
            // Alert after commit signal
            Assert.Contains("AlertEvaluationFailed", src, StringComparison.Ordinal);
            Assert.Contains("CommitAsync", src, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminActorContextAccessor_DoesNotGrantBusinessPermission()
        {
            var accessor = new AdminActorContextAccessor();
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("StaffId", "5"),
                new Claim("StoreId", "2"),
                new Claim(ClaimTypes.Role, "StoreManager")
            }, "test");
            var user = new ClaimsPrincipal(identity);
            var ctx = accessor.Get(user);
            Assert.Equal(5, ctx.StaffId);
            Assert.Equal(2, ctx.StoreId);
            Assert.Contains("StoreManager", ctx.RoleNames);
            // No Can* methods on DTO — claims only
            Assert.Null(ctx.GetType().GetMethod("CanConfirm"));
            Assert.Null(ctx.GetType().GetMethod("Authorize"));
        }

        [Fact]
        public void AdminAuthorization_StoreScopePreservedAfterRefactor()
        {
            var receipt = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminBranchReceiptsController.cs"));
            var restock = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminRestockRequestsController.cs"));
            Assert.Contains("IAdminActorContextAccessor", receipt, StringComparison.Ordinal);
            Assert.Contains("IAdminActorContextAccessor", restock, StringComparison.Ordinal);
            Assert.Contains("HasEffectivePermissionAsync", receipt, StringComparison.Ordinal);
            Assert.Contains("PermissionConstants.ReceiptConfirm", receipt, StringComparison.Ordinal);
            Assert.Contains("PermissionConstants.RestockUpdate", restock, StringComparison.Ordinal);
            Assert.DoesNotContain("RoleConstants", receipt, StringComparison.Ordinal);
            Assert.DoesNotContain("RoleConstants", restock, StringComparison.Ordinal);
            // Service still owns store scope enforcement
            var svc = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Application", "Services", "Inventories", "BranchReceiptService.cs"));
            Assert.Contains("AuthorizeReceiptAccess", svc, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminShared_StatusDescriptor_UsesVietnameseSemanticText()
        {
            var active = AdminStatusDisplay.RecipeActive(true);
            Assert.Equal("Hoạt động", active.Label);
            var restock = AdminStatusDisplay.RestockRequest("PARTIALLY_RECEIVED");
            Assert.Equal("Đã nhận một phần", restock.Label);
            var receipt = AdminStatusDisplay.BranchReceipt("CONFIRMED");
            Assert.Equal("Đã xác nhận", receipt.Label);
        }

        [Fact]
        public void AdminShared_QuantityUnitPartial_HasAccessibleLabel()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "Shared", "_QuantityWithUnit.cshtml");
            var html = File.ReadAllText(path);
            Assert.Contains("aria-label", html, StringComparison.Ordinal);
            Assert.Contains("font-monospace", html, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminShared_EmptyState_RendersPrimaryAction()
        {
            var path = Path.Combine(FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "Shared", "_EmptyState.cshtml");
            var html = File.ReadAllText(path);
            Assert.Contains("ActionLabel", html, StringComparison.Ordinal);
            Assert.Contains("ShowAction", html, StringComparison.Ordinal);
            Assert.Contains("btn-rb-save", html, StringComparison.Ordinal);
        }

        [Fact]
        public void AdminRefactor_RoutesRemainUnchanged()
        {
            // Controllers keep standard action names; area Admin
            var recipeMethods = typeof(AdminRecipeController).GetMethods()
                .Select(m => m.Name)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Contains("Index", recipeMethods);
            Assert.Contains("Create", recipeMethods);
            Assert.Contains("Edit", recipeMethods);
            Assert.Contains("Visualize", recipeMethods);
            Assert.Contains("GetRecipeTree", recipeMethods);
            Assert.Contains("Evaluate", typeof(AdminUnitConversionController).GetMethods().Select(m => m.Name));
            Assert.Contains("Confirm", typeof(AdminBranchReceiptsController).GetMethods().Select(m => m.Name));
            Assert.Contains("StartProcessing", typeof(AdminRestockRequestsController).GetMethods().Select(m => m.Name));
        }

        [Fact]
        public void AdminRefactor_NoNewFrontendDependency()
        {
            var pkg = Path.Combine(FindRepoRoot(), "CafeChain", "package.json");
            if (File.Exists(pkg))
            {
                var text = File.ReadAllText(pkg);
                Assert.DoesNotContain("\"react\"", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"vue\"", text, StringComparison.OrdinalIgnoreCase);
            }
            var ajax = Path.Combine(FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "shared", "admin-ajax.js");
            Assert.True(File.Exists(ajax));
            var ajaxSrc = File.ReadAllText(ajax);
            Assert.Contains("CafeChainAdminAjax", ajaxSrc, StringComparison.Ordinal);
            Assert.DoesNotContain("require(", ajaxSrc, StringComparison.Ordinal);
        }
    }
}
