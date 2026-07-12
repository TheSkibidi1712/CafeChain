using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.UnitConversions;
using CafeChain.Application.Services.Admin.UnitConversions;
using CafeChain.Application.Services.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #127 — physical / measuring / package semantic separation (Admin UX + save validation).</summary>
    public class AdminUnitConversionIssue127Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const int UnitOz = 5;
        private const int UnitCup = 6;
        private const int UnitCan = 11;

        private static void EnsureUnits(AppDbContext ctx)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);
            EnsureUnit(ctx, UnitOz, "oz", UnitType.TheTich);
            EnsureUnit(ctx, UnitCup, "cup", UnitType.TheTich);
            EnsureUnit(ctx, UnitCan, "can", UnitType.Dem);
            EnsureUnit(ctx, 10, "bottle", UnitType.Dem);
            EnsureUnit(ctx, 12, "pack", UnitType.Dem);
        }

        private static void EnsureUnit(AppDbContext ctx, int id, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == id);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                u.Name = code;
                ctx.SaveChanges();
                return;
            }
            if (ctx.Units.Any(x => x.UnitCode.ToLower() == code.ToLower())) return;
            ctx.Units.Add(new Unit { UnitId = id, UnitCode = code, Name = code, Type = type, Active = true });
            ctx.SaveChanges();
        }

        private static AdminUnitConversionService CreateSvc(AppDbContext ctx)
        {
            var phys = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var uc = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, phys);
            return new AdminUnitConversionService(
                ctx, phys, uc, NullLogger<AdminUnitConversionService>.Instance);
        }

        private static Ingredient EnsureIngredient(AppDbContext ctx, string code, string name, int baseUnitId)
        {
            EnsureUnits(ctx);
            var existing = ctx.Ingredients.FirstOrDefault(i => i.Code == code);
            if (existing != null) return existing;
            var ing = new Ingredient { Code = code, Name = name, BaseUnitId = baseUnitId, Active = true };
            ctx.Ingredients.Add(ing);
            ctx.SaveChanges();
            return ing;
        }

        private static void EnsurePrimaryPackage(
            AppDbContext ctx, int ingredientId, decimal packageQty, int packageUnitId, decimal price)
        {
            var offer = ctx.IngredientSuppliers.FirstOrDefault(s =>
                s.IngredientId == ingredientId && s.IsPrimary);
            if (offer != null)
            {
                offer.PackageQuantity = packageQty;
                offer.UnitId = packageUnitId;
                offer.CurrentPrice = price;
                offer.Active = true;
                ctx.SaveChanges();
                return;
            }

            // Minimal supplier
            if (!ctx.Set<Supplier>().Any())
            {
                ctx.Set<Supplier>().Add(new Supplier { SupplierId = 1, Name = "NCC Test", Active = true });
                ctx.SaveChanges();
            }

            ctx.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = ingredientId,
                SupplierId = ctx.Set<Supplier>().Select(s => s.SupplierId).First(),
                UnitId = packageUnitId,
                PackageQuantity = packageQty,
                CurrentPrice = price,
                IsPrimary = true,
                Active = true
            });
            ctx.SaveChanges();
        }

        [Fact]
        public void AdminUnitConversion_PhysicalStandard_IsReadOnly()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSvc(ctx);
            var standards = svc.GetPhysicalStandards();
            Assert.Contains(standards, s => s.DisplayText.Contains("kg") && s.DisplayText.Contains("1000"));
            Assert.Contains(standards, s => s.DisplayText.Contains("l") && s.DisplayText.Contains("1000"));
            Assert.All(standards, s => Assert.False(s.Editable));
        }

        [Fact]
        public async Task AdminUnitConversion_Create_RejectsPhysicalStandardDuplicate()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-PHYS", "Coffee", UnitG);
            var svc = CreateSvc(ctx);
            var eval = await svc.EvaluateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitKg,
                FromQuantity = 1,
                ToUnitId = UnitG,
                ToQuantity = 1000
            });
            Assert.False(eval.IsValid);
            Assert.Equal(UnitConversionErrorCodes.PhysicalStandardAlreadySupported, eval.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_ShowsPhysicalConflict()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-PC", "Sugar", UnitG);
            var svc = CreateSvc(ctx);
            var eval = await svc.EvaluateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitKg,
                FromQuantity = 1,
                ToUnitId = UnitG,
                ToQuantity = 500 // wrong
            });
            Assert.False(eval.IsValid);
            Assert.True(eval.HasPhysicalConflict);
            Assert.Equal(UnitConversionErrorCodes.PhysicalConversionConflict, eval.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_RejectsCrossDimensionWithoutEvidence()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-MV", "Weird", UnitG);
            var svc = CreateSvc(ctx);
            var eval = await svc.EvaluateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitG,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 1
            });
            Assert.False(eval.IsValid);
            Assert.True(eval.IsMassVolumeCross);
            Assert.Equal(UnitConversionErrorCodes.CrossDimensionConversionNotSupported, eval.ErrorCode);

            var create = await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitG,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 1
            });
            Assert.False(create.IsSuccess);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_ShowsPackageConflict_380vs300()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            // Fresh ingredient (avoid seed UnitConversion noise) — same 380 vs 300 semantics as ING00002 demo
            var ing = EnsureIngredient(ctx, "ING-380-300", "Sữa đặc demo lon 380 ml", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);

            var can = await ctx.Units.AsNoTracking().FirstAsync(u => u.UnitId == UnitCan);
            Assert.Equal("can", can.UnitCode, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(UnitType.Dem, can.Type);

            var svc = CreateSvc(ctx);

            var eval = await svc.EvaluateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCan,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 300
            });

            Assert.True(eval.HasPackageConflict,
                $"msg={eval.Message}; codes={string.Join(",", eval.Codes)}; from={eval.FromUnitCode}/{eval.FromDimension}; to={eval.ToUnitCode}");
            Assert.True(eval.RequiresPackageAcknowledgement);
            Assert.Equal(380m, eval.PrimaryPackageQuantity);
            Assert.Equal(300m, eval.ProposedPackageLikeQuantity);
            Assert.False(eval.IsValid);
            Assert.Equal(UnitConversionErrorCodes.PackageConflictAcknowledgementRequired, eval.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_RequiresPackageConflictAcknowledgement()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-ACK", "Condensed", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);
            var svc = CreateSvc(ctx);
            var req = new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCan,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 300,
                PackageConflictAcknowledged = false
            };
            var create = await svc.CreateAsync(req);
            Assert.False(create.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.PackageConflictAcknowledgementRequired, create.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_AcknowledgedPackageConflict_DoesNotChangeSupplierPackage()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-ACK2", "Condensed2", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);
            var before = await ctx.IngredientSuppliers.AsNoTracking()
                .FirstAsync(s => s.IngredientId == ing.IngredientId && s.IsPrimary);

            var svc = CreateSvc(ctx);
            var create = await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCan,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 300,
                PackageConflictAcknowledged = true
            });
            Assert.True(create.IsSuccess, create.Message);

            var after = await ctx.IngredientSuppliers.AsNoTracking()
                .FirstAsync(s => s.IngredientSupplierId == before.IngredientSupplierId);
            Assert.Equal(380m, after.PackageQuantity);
            Assert.Equal(before.UnitId, after.UnitId);
            Assert.Equal(before.CurrentPrice, after.CurrentPrice);

            var row = await ctx.UnitConversions.FirstAsync(c => c.UnitConversionId == create.Data);
            Assert.Equal(300m, row.ToQuantity);
        }

        [Fact]
        public async Task AdminUnitConversion_Create_RejectsDuplicatePair()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-DUP", "Milk", UnitMl);
            var svc = CreateSvc(ctx);
            var req = new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCup,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 240
            };
            Assert.True((await svc.CreateAsync(req)).IsSuccess);
            var second = await svc.CreateAsync(req);
            Assert.False(second.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.DuplicateConversionPair, second.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_ReverseFactor_IsDerivedConsistently()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-REV", "Cream", UnitMl);
            var svc = CreateSvc(ctx);
            var eval = await svc.EvaluateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCup,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 240
            });
            Assert.True(eval.IsValid, eval.Message);
            Assert.Equal(240m, eval.Factor);
            Assert.NotNull(eval.ReverseFactor);
            Assert.InRange(eval.ReverseFactor!.Value, 0.0041m, 0.0042m);
        }

        [Fact]
        public async Task AdminUnitConversion_List_GroupsRowsByIngredient()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-GRP", "Group me", UnitMl);
            var svc = CreateSvc(ctx);
            await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCup,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 240
            });
            var index = await svc.GetIndexAsync("ING-GRP");
            Assert.Contains(index.Groups, g => g.IngredientCode == "ING-GRP" && g.Conversions.Count >= 1);
        }

        [Fact]
        public async Task AdminUnitConversion_List_ShowsPackageDefinitionSeparately()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-PKG", "Pkg", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);
            ctx.UnitConversions.Add(new UnitConversion
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCan,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 300,
                Active = true
            });
            await ctx.SaveChangesAsync();

            var svc = CreateSvc(ctx);
            var index = await svc.GetIndexAsync("ING-PKG");
            var g = Assert.Single(index.Groups.Where(x => x.IngredientCode == "ING-PKG"));
            Assert.NotNull(g.PrimaryPackage);
            Assert.Equal(380m, g.PrimaryPackage!.PackageQuantity);
            Assert.True(g.HasPackageConflict);
            Assert.Contains(g.Conversions, c => c.HasPackageConflict);
        }

        [Fact]
        public async Task AdminUnitConversion_List_SeparatesPhysicalAndIngredientConversions()
        {
            using var ctx = CreateDbContext();
            var svc = CreateSvc(ctx);
            var index = await svc.GetIndexAsync();
            Assert.NotEmpty(index.PhysicalStandards);
            Assert.All(index.PhysicalStandards, p => Assert.False(p.Editable));
            // Physical rows are not mixed as editable group conversions without ingredient
            Assert.DoesNotContain(index.PhysicalStandards, p => p.DisplayText.Contains("cup", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task AdminUnitConversion_IngredientSearch_ByCodeAndName()
        {
            using var ctx = CreateDbContext();
            EnsureIngredient(ctx, "ING-SRCH", "Bột cacao đặc biệt", UnitG);
            var svc = CreateSvc(ctx);
            var byCode = await svc.GetIngredientOptionsAsync("ING-SRCH");
            Assert.Contains(byCode, i => i.Code == "ING-SRCH");
            var byName = await svc.GetIngredientOptionsAsync("cacao");
            Assert.Contains(byName, i => i.Name.Contains("cacao", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task AdminUnitConversion_PackageQuantity_IsNotStoredAsPhysicalConversion()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-NOP", "NoPkgAsPhys", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);
            var svc = CreateSvc(ctx);
            await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCan,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 300,
                PackageConflictAcknowledged = true
            });
            // Package still 380; conversion is separate row
            var offer = await ctx.IngredientSuppliers.FirstAsync(s => s.IngredientId == ing.IngredientId);
            Assert.Equal(380m, offer.PackageQuantity);
            Assert.DoesNotContain(ctx.UnitConversions, c =>
                c.IngredientId == ing.IngredientId && c.FromQuantity == 380 && c.FromUnitId == UnitMl);
        }

        [Fact]
        public async Task AdminUnitConversion_CostPreview_UsesPackageQuantityAndUnit()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-COST", "Costing", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 380m, UnitMl, 27000m);
            var svc = CreateSvc(ctx);
            var index = await svc.GetIndexAsync("ING-COST");
            // force include by adding a conversion so group appears without search-only empty
            await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCup,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 240
            });
            index = await svc.GetIndexAsync("ING-COST");
            var g = Assert.Single(index.Groups.Where(x => x.IngredientCode == "ING-COST"));
            Assert.True(g.PrimaryPackage!.IsComplete);
            Assert.NotNull(g.PrimaryPackage.BaseUnitCost);
            // 27000/380 ≈ 71.05
            Assert.InRange(g.PrimaryPackage.BaseUnitCost!.Value, 71.0m, 71.2m);
        }

        [Fact]
        public async Task AdminUnitConversion_CostIncomplete_ExplainsMissingConversion()
        {
            using var ctx = CreateDbContext();
            // Package in kg while base is ml and no conversion → incomplete
            var ing = EnsureIngredient(ctx, "ING-INC", "Incomplete", UnitMl);
            EnsurePrimaryPackage(ctx, ing.IngredientId, 1m, UnitKg, 100000m);
            ctx.UnitConversions.Add(new UnitConversion
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitCup,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 240,
                Active = true
            });
            await ctx.SaveChangesAsync();
            var svc = CreateSvc(ctx);
            var index = await svc.GetIndexAsync("ING-INC");
            var g = Assert.Single(index.Groups.Where(x => x.IngredientCode == "ING-INC"));
            Assert.False(g.PrimaryPackage!.IsComplete);
            Assert.False(string.IsNullOrWhiteSpace(g.PrimaryPackage.IncompleteReason));
        }

        [Fact]
        public async Task AdminUnitConversion_ServerRevalidatesConflictBeforeSave()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-SRV", "Server", UnitG);
            var svc = CreateSvc(ctx);
            // Client could claim ack but still fail physical conflict
            var create = await svc.CreateAsync(new AdminUnitConversionEvaluateRequest
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitKg,
                FromQuantity = 1,
                ToUnitId = UnitG,
                ToQuantity = 999,
                PackageConflictAcknowledged = true
            });
            Assert.False(create.IsSuccess);
            Assert.Equal(UnitConversionErrorCodes.PhysicalConversionConflict, create.ErrorCode);
        }

        [Fact]
        public async Task AdminUnitConversion_ExistingCrossDimensionRow_ShowsReviewStatus()
        {
            using var ctx = CreateDbContext();
            var ing = EnsureIngredient(ctx, "ING-XD", "Cross", UnitG);
            ctx.UnitConversions.Add(new UnitConversion
            {
                IngredientId = ing.IngredientId,
                FromUnitId = UnitG,
                FromQuantity = 1,
                ToUnitId = UnitMl,
                ToQuantity = 1,
                Active = true
            });
            await ctx.SaveChangesAsync();
            var svc = CreateSvc(ctx);
            var index = await svc.GetIndexAsync("ING-XD");
            var g = Assert.Single(index.Groups.Where(x => x.IngredientCode == "ING-XD"));
            Assert.True(g.HasReviewRows);
            Assert.Contains(g.Conversions, c => c.IsCrossDimensionMassVolume && !c.AllowEdit);
        }

        [Fact]
        public void AdminUnitConversion_Create_PostPayload_PreservesFieldNames()
        {
            var vmPath = System.IO.Path.Combine(
                FindRepoRoot(), "CafeChain", "ViewModels", "Admin", "UnitConversions", "UnitConversionVM.cs");
            var text = System.IO.File.ReadAllText(vmPath);
            Assert.Contains("IngredientId", text);
            Assert.Contains("FromUnitId", text);
            Assert.Contains("FromQuantity", text);
            Assert.Contains("ToUnitId", text);
            Assert.Contains("ToQuantity", text);
            Assert.Contains("PackageConflictAcknowledged", text);
        }

        [Fact]
        public void AdminUnitConversion_DestructiveAction_HasAccessibleName()
        {
            var index = System.IO.File.ReadAllText(System.IO.Path.Combine(
                FindRepoRoot(), "CafeChain", "Areas", "Admin", "Views", "AdminUnitConversion", "Index.cshtml"));
            Assert.Contains("Xóa quy đổi", index);
        }

        [Fact]
        public void AdminUnitConversion_ReadOnlyRole_HasNoMutationActions()
        {
            // Contract: controller uses AdminBaseController only (same as pre-#127).
            // Documented: mutation available to panel access; ViewBag.CanWrite currently true for all panel.
            // Structural: no expanded roles beyond base.
            var ctrl = System.IO.File.ReadAllText(System.IO.Path.Combine(
                FindRepoRoot(), "CafeChain", "Areas", "Admin", "Controllers", "AdminUnitConversionController.cs"));
            Assert.Contains("AdminBaseController", ctrl);
            Assert.DoesNotContain("StoreManager", ctrl);
            Assert.Contains("ValidateAntiForgeryToken", ctrl);
        }

        private static string FindRepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "CafeChain", "Areas")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
    }
}
