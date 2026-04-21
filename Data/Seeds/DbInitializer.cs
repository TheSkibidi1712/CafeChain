using CafeChain.Data;
using CafeChain.Models.Staffs;
using CafeChain.Models.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace CafeChain.Data.Seeds
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(AppDbContext context, IWebHostEnvironment env)
        {
            // ========== SEED 1: Dữ liệu Tỉnh/Huyện/Xã ==========
            try
            {
                bool hasProvinces = await context.Provinces.AnyAsync();
                bool hasDistricts = await context.Districts.AnyAsync();

                if (!hasProvinces || !hasDistricts)
                {
                    var sqlFilePath = Path.Combine(env.ContentRootPath, "Data", "Seeds", "vietnam_locations.sql");
                    if (File.Exists(sqlFilePath))
                    {
                        var sqlCode = await File.ReadAllTextAsync(sqlFilePath);
                        await context.Database.ExecuteSqlRawAsync(sqlCode);
                        Console.WriteLine("✅ Đã khởi tạo dữ liệu Tỉnh/Huyện/Xã thành công.");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Không tìm thấy file vietnam_locations.sql tại: " + sqlFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Lỗi khởi tạo dữ liệu địa phương: " + ex.Message);
            }

            // ========== SEED 2: 4 Ca Làm Việc Chuẩn cho mỗi Store ==========
            try
            {
                var allStores = await context.Stores.Where(s => s.Active).ToListAsync();

                foreach (var store in allStores)
                {
                    // Nếu chưa có "Ca 1", tự động tạo bộ 4 ca chuẩn cho 24 giờ
                    if (!context.Shifts.Any(sh => sh.StoreId == store.StoreId && sh.Name == "Ca 1"))
                    {
                        context.Shifts.AddRange(
                            new Shift
                            {
                                Name = "Ca 1",
                                StartTime = new TimeSpan(6, 0, 0),
                                EndTime = new TimeSpan(12, 0, 0),
                                IsOvernight = false,
                                IsFreeShift = false,
                                Duration = TimeSpan.FromHours(6),
                                Active = true,
                                StoreId = store.StoreId,
                                Notes = "06:00 - 12:00"
                            },
                            new Shift
                            {
                                Name = "Ca 2",
                                StartTime = new TimeSpan(12, 0, 0),
                                EndTime = new TimeSpan(18, 0, 0),
                                IsOvernight = false,
                                IsFreeShift = false,
                                Duration = TimeSpan.FromHours(6),
                                Active = true,
                                StoreId = store.StoreId,
                                Notes = "12:00 - 18:00"
                            },
                            new Shift
                            {
                                Name = "Ca 3",
                                StartTime = new TimeSpan(18, 0, 0),
                                EndTime = new TimeSpan(23, 0, 0),
                                IsOvernight = false,
                                IsFreeShift = false,
                                Duration = TimeSpan.FromHours(5),
                                Active = true,
                                StoreId = store.StoreId,
                                Notes = "18:00 - 23:00"
                            },
                            new Shift
                            {
                                Name = "Ca 4",
                                StartTime = new TimeSpan(22, 0, 0),
                                EndTime = new TimeSpan(6, 0, 0),
                                IsOvernight = true,
                                IsFreeShift = false,
                                Duration = TimeSpan.FromHours(8),
                                Active = true,
                                StoreId = store.StoreId,
                                Notes = "22:00 - 06:00 (Hôm sau)"
                            }
                        );
                        Console.WriteLine($"✅ Đã tạo 4 ca (1, 2, 3, 4) cho cửa hàng: {store.Name}");
                    }
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Lỗi khởi tạo ca làm việc: " + ex.Message);
            }

            // ========== SEED 3: Dữ liệu mẫu cho BOM Ecosystem ==========
            try
            {
                // --- 3.1 UNITS (Đơn vị tính) ---
                if (!await context.Units.AnyAsync())
                {
                    context.Units.AddRange(
                        new Unit { UnitCode = "g", Name = "Gram", Type = UnitType.KhoiLuong, Active = true },
                        new Unit { UnitCode = "kg", Name = "Kilogram", Type = UnitType.KhoiLuong, Active = true },
                        new Unit { UnitCode = "ml", Name = "Mililít", Type = UnitType.TheTich, Active = true },
                        new Unit { UnitCode = "l", Name = "Lít", Type = UnitType.TheTich, Active = true },
                        new Unit { UnitCode = "pcs", Name = "Cái/Miếng", Type = UnitType.Dem, Active = true },
                        new Unit { UnitCode = "hop", Name = "Hộp", Type = UnitType.Dem, Active = true }
                    );
                    await context.SaveChangesAsync();
                    Console.WriteLine("✅ Đã tạo 6 đơn vị tính (g, kg, ml, l, pcs, hộp)");
                }

                var unitG = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "g");
                var unitKg = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "kg");
                var unitMl = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "ml");
                var unitL = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "l");
                var unitPcs = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "pcs");
                var unitHop = await context.Units.FirstOrDefaultAsync(u => u.UnitCode == "hop");

                if (unitG == null || unitKg == null || unitMl == null || unitL == null)
                {
                    Console.WriteLine("⚠️ Không tìm thấy đơn vị tính cơ bản. Bỏ qua seed BOM.");
                    return;
                }

                // --- 3.2 SUPPLIER (Nhà cung cấp mẫu) ---
                Supplier mockSupplier;
                if (!await context.Suppliers.AnyAsync(s => s.Name == "NCC Mẫu F&B"))
                {
                    mockSupplier = new Supplier
                    {
                        Name = "NCC Mẫu F&B",
                        Code = "SUP00001",
                        Address = "123 Nguyễn Huệ, Q1, TP.HCM",
                        Active = true,
                        TaxCode = "0312345678"
                    };
                    context.Suppliers.Add(mockSupplier);
                    await context.SaveChangesAsync();
                    Console.WriteLine("✅ Đã tạo NCC mẫu: NCC Mẫu F&B");
                }
                else
                {
                    mockSupplier = await context.Suppliers.FirstAsync(s => s.Name == "NCC Mẫu F&B");
                }

                // --- 3.3 INGREDIENTS + PRICES ---
                if (!await context.Ingredients.AnyAsync(i => i.Code == "ING00001"))
                {
                    var ingredients = new List<(string Code, string Name, int BaseUnitId, decimal Price)>
                    {
                        ("ING00001", "Trà đen (Lá)", unitG.UnitId, 120),         // 120đ/g = 120.000đ/kg
                        ("ING00002", "Đường trắng", unitG.UnitId, 25),            // 25đ/g = 25.000đ/kg
                        ("ING00003", "Sữa đặc Ông Thọ", unitMl.UnitId, 52),      // 52đ/ml  
                        ("ING00004", "Sữa tươi TH True Milk", unitMl.UnitId, 28), // 28đ/ml
                        ("ING00005", "Trân châu đen", unitG.UnitId, 65),          // 65đ/g
                        ("ING00006", "Đá viên", unitG.UnitId, 5),                 // 5đ/g
                        ("ING00007", "Bột kem béo (Creamer)", unitG.UnitId, 85),  // 85đ/g
                        ("ING00008", "Syrup Caramel", unitMl.UnitId, 95),         // 95đ/ml
                        ("ING00009", "Hạt Cà phê Robusta", unitG.UnitId, 180),    // 180đ/g
                        ("ING00010", "Nước lọc", unitMl.UnitId, 1),               // 1đ/ml
                    };

                    foreach (var (code, name, baseUnitId, price) in ingredients)
                    {
                        var ing = new Ingredient
                        {
                            Code = code,
                            Name = name,
                            BaseUnitId = baseUnitId,
                            Active = true
                        };
                        context.Ingredients.Add(ing);
                        await context.SaveChangesAsync();

                        // Gắn giá NCC
                        context.IngredientSuppliers.Add(new IngredientSupplier
                        {
                            IngredientId = ing.IngredientId,
                            SupplierId = mockSupplier.SupplierId,
                            Price = price,
                            UnitId = baseUnitId,
                            IsPrimary = true
                        });
                    }
                    await context.SaveChangesAsync();
                    Console.WriteLine("✅ Đã tạo 10 nguyên liệu mẫu + giá vốn NCC");
                }

                // --- 3.4 UNIT CONVERSIONS ---
                if (!await context.UnitConversions.AnyAsync())
                {
                    var ingTraDen = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00001");
                    var ingSuaDac = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00003");
                    var ingSuaTuoi = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00004");
                    var ingCaPhe = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00009");
                    var ingDuong = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00002");

                    if (ingTraDen != null && ingSuaDac != null && unitKg != null && unitHop != null)
                    {
                        context.UnitConversions.AddRange(
                            new UnitConversion { IngredientId = ingTraDen.IngredientId, FromUnitId = unitKg.UnitId, FromQuantity = 1, ToUnitId = unitG.UnitId, ToQuantity = 1000 },
                            new UnitConversion { IngredientId = ingSuaDac.IngredientId, FromUnitId = unitHop.UnitId, FromQuantity = 1, ToUnitId = unitMl.UnitId, ToQuantity = 380 },
                            new UnitConversion { IngredientId = ingSuaTuoi.IngredientId, FromUnitId = unitL.UnitId, FromQuantity = 1, ToUnitId = unitMl.UnitId, ToQuantity = 1000 },
                            new UnitConversion { IngredientId = ingCaPhe.IngredientId, FromUnitId = unitKg.UnitId, FromQuantity = 1, ToUnitId = unitG.UnitId, ToQuantity = 1000 },
                            new UnitConversion { IngredientId = ingDuong.IngredientId, FromUnitId = unitKg.UnitId, FromQuantity = 1, ToUnitId = unitG.UnitId, ToQuantity = 1000 }
                        );
                        await context.SaveChangesAsync();
                        Console.WriteLine("✅ Đã tạo 5 quy đổi đơn vị mẫu");
                    }
                }

                // --- 3.5 SAMPLE RECIPES (Công thức mẫu) ---
                if (!await context.Recipes.AnyAsync(r => r.Name == "Cốt Trà Đen (1 Lít)"))
                {
                    var ingTraDen = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00001");
                    var ingDuong = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00002");
                    var ingNuoc = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00010");

                    if (ingTraDen != null && ingDuong != null && ingNuoc != null)
                    {
                        // Công thức 1: Cốt Trà Đen (Bán thành phẩm)
                        var recipeCotTra = new Recipe
                        {
                            Name = "Cốt Trà Đen (1 Lít)",
                            YieldPercentage = 100,
                            Active = true,
                            RecipeDetails = new List<RecipeDetail>
                            {
                                new RecipeDetail { IngredientId = ingTraDen.IngredientId, Quantity = 30, UnitId = unitG.UnitId },   // 30g trà
                                new RecipeDetail { IngredientId = ingDuong.IngredientId, Quantity = 100, UnitId = unitG.UnitId },   // 100g đường
                                new RecipeDetail { IngredientId = ingNuoc.IngredientId, Quantity = 1000, UnitId = unitMl.UnitId },  // 1000ml nước
                            }
                        };
                        context.Recipes.Add(recipeCotTra);
                        await context.SaveChangesAsync();
                        Console.WriteLine("✅ Đã tạo công thức mẫu: Cốt Trà Đen (1 Lít)");

                        // Công thức 2: Trà Sữa Trân Châu (Món bán POS)
                        var ingSuaDac = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00003");
                        var ingTranChau = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00005");
                        var ingDa = await context.Ingredients.FirstOrDefaultAsync(i => i.Code == "ING00006");

                        var recipeTraSua = new Recipe
                        {
                            Name = "Trà Sữa Trân Châu (Size M)",
                            YieldPercentage = 100,
                            Active = true,
                            RecipeDetails = new List<RecipeDetail>
                            {
                                // Dùng Cốt Trà Đen (sub-recipe)
                                new RecipeDetail { ChildRecipeId = recipeCotTra.RecipeId, IngredientId = null, Quantity = 200, UnitId = unitMl.UnitId },
                                new RecipeDetail { IngredientId = ingSuaDac?.IngredientId, Quantity = 30, UnitId = unitMl.UnitId },
                                new RecipeDetail { IngredientId = ingTranChau?.IngredientId, Quantity = 50, UnitId = unitG.UnitId },
                                new RecipeDetail { IngredientId = ingDa?.IngredientId, Quantity = 150, UnitId = unitG.UnitId },
                            }
                        };
                        context.Recipes.Add(recipeTraSua);
                        await context.SaveChangesAsync();
                        Console.WriteLine("✅ Đã tạo công thức mẫu: Trà Sữa Trân Châu (Size M)");
                    }
                }

                Console.WriteLine("✅ Seed BOM Ecosystem hoàn tất!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Lỗi khởi tạo BOM Ecosystem: " + ex.Message);
            }
        }
    }
}
