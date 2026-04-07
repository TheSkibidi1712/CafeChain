using CafeChain.Models;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== PRODUCT TYPE ==========================
    public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
    {
        public void Configure(EntityTypeBuilder<ProductType> entity)
        {
            entity.ToTable("ProductTypes");

            entity.HasKey(x => x.ProductTypeId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Code).IsUnique();

            // 🔥 SEED QUAN TRỌNG
            entity.HasData(
                new ProductType
                {
                    ProductTypeId = 1,
                    Code = "HANDCRAFTED",
                    Name = "Pha chế",
                    Active = true
                },
                new ProductType
                {
                    ProductTypeId = 2,
                    Code = "RETAIL",
                    Name = "Đóng chai",
                    Active = true
                }
            );
        }
    }

    // ========================== DRINK ==========================
    public class DrinkConfiguration : IEntityTypeConfiguration<Drink>
    {
        public void Configure(EntityTypeBuilder<Drink> entity)
        {
            entity.ToTable("Drinks");

            entity.HasKey(x => x.DrinkId);

            // ================= PROPERTIES =================
            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATIONSHIPS =================

            // Category
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Drinks)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ProductType (🔥 thiếu trong config cũ)
            entity.HasOne(x => x.ProductType)
                .WithMany(x => x.Drinks)
                .HasForeignKey(x => x.ProductTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasIndex(x => new { x.CategoryId, x.ProductTypeId });

            // ================= SEED DATA =================
            entity.HasData(
                new Drink
                {
                    DrinkId = 1,
                    CategoryId = 1,
                    ProductTypeId = 1,
                    Name = "Cà phê sữa",
                    Description = "Cà phê pha với sữa đặc.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Drink
                {
                    DrinkId = 2,
                    CategoryId = 1,
                    ProductTypeId = 1,
                    Name = "Cà phê đen",
                    Description = "Cà phê pha với nước sôi, không có sữa.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Drink
                {
                    DrinkId = 3,
                    CategoryId = 2,
                    ProductTypeId = 1,
                    Name = "Trà sữa truyền thống",
                    Description = "Trà sữa pha với trân châu đen và đá viên.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Drink
                {
                    DrinkId = 4,
                    CategoryId = 2,
                    ProductTypeId = 1,
                    Name = "Trà sữa socola",
                    Description = "Trà sữa socola thơm ngon, béo ngậy.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Drink
                {
                    DrinkId = 5,
                    CategoryId = 3,
                    ProductTypeId = 2,
                    Name = "Sting",
                    Description = "Sting mát lạnh",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Drink
                {
                    DrinkId = 6,
                    CategoryId = 3,
                    ProductTypeId = 2,
                    Name = "Coca-cola",
                    Description = "Coca-cola mát lạnh",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }

    // ========================== DRINK CATEGORY ==========================
    public class DrinkCategoryConfiguration : IEntityTypeConfiguration<DrinkCategory>
    {
        public void Configure(EntityTypeBuilder<DrinkCategory> entity)
        {
            entity.ToTable("DrinkCategories");

            entity.HasKey(x => x.CategoryId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new DrinkCategory { CategoryId = 1, Name = "Coffee", Active = true },
                new DrinkCategory { CategoryId = 2, Name = "Trà sữa", Active = true },
                new DrinkCategory { CategoryId = 3, Name = "Nước ngọt", Active = true }
            );
        }
    }

    // ========================== DRINK IMAGE ==========================
    public class DrinkImageConfiguration : IEntityTypeConfiguration<DrinkImage>
    {
        public void Configure(EntityTypeBuilder<DrinkImage> entity)
        {
            entity.ToTable("DrinkImages");

            entity.HasKey(x => x.DrinkImageId);

            entity.Property(x => x.IsDefault)
                .HasDefaultValue(false);

            entity.Property(x => x.ImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkImages)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new DrinkImage { DrinkImageId = 1, DrinkId = 1, IsDefault = true, ImageUrl = "/Images/DrinkImages/cps1.jpg" },
                new DrinkImage { DrinkImageId = 2, DrinkId = 1, IsDefault = false, ImageUrl = "/Images/DrinkImages/cps2.jpg" },
                new DrinkImage { DrinkImageId = 3, DrinkId = 1, IsDefault = false, ImageUrl = "/Images/DrinkImages/cps3.jpg" },
                new DrinkImage { DrinkImageId = 4, DrinkId = 1, IsDefault = false, ImageUrl = "/Images/DrinkImages/cps4.jpg" },
                new DrinkImage { DrinkImageId = 5, DrinkId = 2, IsDefault = true, ImageUrl = "/Images/DrinkImages/cpd1.jpg" },
                new DrinkImage { DrinkImageId = 6, DrinkId = 2, IsDefault = false, ImageUrl = "/Images/DrinkImages/cpd2.jpg" },
                new DrinkImage { DrinkImageId = 7, DrinkId = 2, IsDefault = false, ImageUrl = "/Images/DrinkImages/cpd3.jpg" },
                new DrinkImage { DrinkImageId = 8, DrinkId = 2, IsDefault = false, ImageUrl = "/Images/DrinkImages/cpd4.jpg" },
                new DrinkImage { DrinkImageId = 9, DrinkId = 3, IsDefault = true, ImageUrl = "/Images/DrinkImages/trasuatranchauden1.jpg" },
                new DrinkImage { DrinkImageId = 10, DrinkId = 3, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuatranchauden2.jpg" },
                new DrinkImage { DrinkImageId = 11, DrinkId = 3, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuatranchauden3.jpg" },
                new DrinkImage { DrinkImageId = 12, DrinkId = 3, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuatranchauden4.jpg" },
                new DrinkImage { DrinkImageId = 13, DrinkId = 4, IsDefault = true, ImageUrl = "/Images/DrinkImages/trasuasocola1.jpg" },
                new DrinkImage { DrinkImageId = 14, DrinkId = 4, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuasocola2.jpg" },
                new DrinkImage { DrinkImageId = 15, DrinkId = 4, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuasocola3.jpg" },
                new DrinkImage { DrinkImageId = 16, DrinkId = 4, IsDefault = false, ImageUrl = "/Images/DrinkImages/trasuasocola4.jpg" },
                new DrinkImage { DrinkImageId = 17, DrinkId = 5, IsDefault = true, ImageUrl = "/Images/DrinkImages/sting1.jpg" },
                new DrinkImage { DrinkImageId = 18, DrinkId = 5, IsDefault = false, ImageUrl = "/Images/DrinkImages/sting2.jpg" },
                new DrinkImage { DrinkImageId = 19, DrinkId = 5, IsDefault = false, ImageUrl = "/Images/DrinkImages/sting3.jpg" },
                new DrinkImage { DrinkImageId = 20, DrinkId = 5, IsDefault = false, ImageUrl = "/Images/DrinkImages/sting4.jpg" },
                new DrinkImage { DrinkImageId = 21, DrinkId = 6, IsDefault = true, ImageUrl = "/Images/DrinkImages/coca1.jpg" },
                new DrinkImage { DrinkImageId = 22, DrinkId = 6, IsDefault = false, ImageUrl = "/Images/DrinkImages/coca2.jpg" },
                new DrinkImage { DrinkImageId = 23, DrinkId = 6, IsDefault = false, ImageUrl = "/Images/DrinkImages/coca3.jpg" },
                new DrinkImage { DrinkImageId = 24, DrinkId = 6, IsDefault = false, ImageUrl = "/Images/DrinkImages/coca4.jpg" }
            );
        }
    }

    // ========================== DRINK SIZE ==========================
    public class DrinkSizeConfiguration : IEntityTypeConfiguration<DrinkSize>
    {
        public void Configure(EntityTypeBuilder<DrinkSize> entity)
        {
            entity.ToTable("DrinkSizes");

            entity.HasKey(x => x.DrinkSizeId);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Size)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.SizeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DrinkId, x.SizeId })
                .IsUnique();

            entity.HasData(
                new DrinkSize { DrinkSizeId = 1, DrinkId = 1, SizeId = 1, Price = 30000m, Active = true },
                new DrinkSize { DrinkSizeId = 3, DrinkId = 2, SizeId = 1, Price = 22000m, Active = true },
                new DrinkSize { DrinkSizeId = 5, DrinkId = 3, SizeId = 1, Price = 22000m, Active = true },
                new DrinkSize { DrinkSizeId = 6, DrinkId = 3, SizeId = 2, Price = 27000m, Active = true },
                new DrinkSize { DrinkSizeId = 7, DrinkId = 3, SizeId = 3, Price = 32000m, Active = true },
                new DrinkSize { DrinkSizeId = 8, DrinkId = 4, SizeId = 1, Price = 25000m, Active = true },
                new DrinkSize { DrinkSizeId = 9, DrinkId = 4, SizeId = 2, Price = 30000m, Active = true },
                new DrinkSize { DrinkSizeId = 10, DrinkId = 4, SizeId = 3, Price = 35000m, Active = true },
                new DrinkSize { DrinkSizeId = 11, DrinkId = 5, SizeId = 5, Price = 15000m, Active = true },
                new DrinkSize { DrinkSizeId = 12, DrinkId = 5, SizeId = 6, Price = 20000m, Active = true },
                new DrinkSize { DrinkSizeId = 13, DrinkId = 5, SizeId = 7, Price = 15000m, Active = true },
                new DrinkSize { DrinkSizeId = 14, DrinkId = 6, SizeId = 5, Price = 15000m, Active = true },
                new DrinkSize { DrinkSizeId = 15, DrinkId = 6, SizeId = 6, Price = 20000m, Active = true },
                new DrinkSize { DrinkSizeId = 16, DrinkId = 6, SizeId = 7, Price = 25000m, Active = true },
                new DrinkSize { DrinkSizeId = 17, DrinkId = 6, SizeId = 8, Price = 30000m, Active = true }

            );
        }
    }


    // ========================== SIZE ==========================
    public class SizeConfiguration : IEntityTypeConfiguration<Size>
    {
        public void Configure(EntityTypeBuilder<Size> entity)
        {
            entity.ToTable("Sizes");

            entity.HasKey(x => x.SizeId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Description)
                .HasMaxLength(300);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new Size { SizeId = 1, Name = "S", Description = "Kích thước nhỏ", Active = true },
                new Size { SizeId = 2, Name = "M", Description = "Kích thước trung bình", Active = true },
                new Size { SizeId = 3, Name = "L", Description = "Kích thước lớn", Active = true },
                new Size { SizeId = 4, Name = "XL", Description = "Kích thước rất lớn", Active = true },
                new Size { SizeId = 5, Name = "150ml", Description = "Kích thước 150ml", Active = true },
                new Size { SizeId = 6, Name = "200ml", Description = "Kích thước 200ml", Active = true },
                new Size { SizeId = 7, Name = "250ml", Description = "Kích thước 250ml", Active = true },
                new Size { SizeId = 8, Name = "300ml", Description = "Kích thước 300ml", Active = true }
            );
        }
    }

    // ========================== DRINK TOPPING ==========================
    public class DrinkToppingConfiguration : IEntityTypeConfiguration<DrinkTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkTopping> entity)
        {
            entity.ToTable("DrinkToppings");

            entity.HasKey(x => x.DrinkToppingId);

            // Bỏ config Active vì thuộc tính này không có trong DB và đã Set NotMapped


            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DrinkId, x.ToppingId })
                .IsUnique();

            entity.HasData(
                new DrinkTopping { DrinkToppingId = 1, DrinkId = 3 , ToppingId = 1 },
                new DrinkTopping { DrinkToppingId = 2, DrinkId = 3 , ToppingId = 2 },
                new DrinkTopping { DrinkToppingId = 3, DrinkId = 3 , ToppingId = 3 },
                new DrinkTopping { DrinkToppingId = 4, DrinkId = 3 , ToppingId = 4 },
                new DrinkTopping { DrinkToppingId = 5, DrinkId = 3 , ToppingId = 5 },
                new DrinkTopping { DrinkToppingId = 6, DrinkId = 3 , ToppingId = 6 },

                new DrinkTopping { DrinkToppingId = 7, DrinkId = 4 , ToppingId = 1 },
                new DrinkTopping { DrinkToppingId = 8, DrinkId = 4 , ToppingId = 2 },
                new DrinkTopping { DrinkToppingId = 9, DrinkId = 4 , ToppingId = 3 },
                new DrinkTopping { DrinkToppingId = 10, DrinkId = 4 , ToppingId = 4 },
                new DrinkTopping { DrinkToppingId = 11, DrinkId = 4 , ToppingId = 5 },
                new DrinkTopping { DrinkToppingId = 12, DrinkId = 4 , ToppingId = 6 }
            );
        }
    }

    // ========================== DRINK DEFAULT TOPPING ==========================
    public class DrinkDefaultToppingConfiguration : IEntityTypeConfiguration<DrinkDefaultTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkDefaultTopping> entity)
        {
            entity.ToTable("DrinkDefaultToppings");

            // ================= KEY =================
            entity.HasKey(x => x.DrinkDefaultToppingId);

            // ================= RELATIONSHIPS =================

            entity.HasOne(x => x.Drink)
                .WithMany(d => d.DrinkDefaultToppings) // 👈 nếu chưa add navigation bên Drink
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany() // 👈 nếu chưa add navigation bên Topping
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.DrinkId, x.ToppingId })
                .IsUnique();

            // ================= SEED DATA =================

            entity.HasData(
                new DrinkDefaultTopping { DrinkDefaultToppingId = 1, DrinkId = 4, ToppingId = 1 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 2, DrinkId = 4, ToppingId = 2 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 3, DrinkId = 4, ToppingId = 3 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 4, DrinkId = 4, ToppingId = 4 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 5, DrinkId = 4, ToppingId = 5 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 6, DrinkId = 4, ToppingId = 6 }
            );
        }
    }

    // ========================== TOPPING ==========================
    public class ToppingConfiguration : IEntityTypeConfiguration<Topping>
    {
        public void Configure(EntityTypeBuilder<Topping> entity)
        {
            entity.ToTable("Toppings");

            entity.HasKey(x => x.ToppingId);

            // ================= PROPERTIES =================

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.ImageUrl)
                .HasMaxLength(500); // 👈 thêm mới

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Name)
                .IsUnique();

            // ================= RELATIONSHIPS =================

            entity.HasMany(x => x.DrinkToppings)
                .WithOne(x => x.Topping)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.StoreToppings)
                .WithOne(x => x.Topping)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.OrderToppings)
                .WithOne(x => x.Topping)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new Topping { ToppingId = 1, Name = "Trân châu đen", Price = 5000m, ImageUrl = "/Images/ToppingImages/tranchauden.jpg", Active = true },
                new Topping { ToppingId = 2, Name = "Trân châu trắng", Price = 5000m, ImageUrl = "/Images/ToppingImages/tranchautrang.jpg", Active = true },
                new Topping { ToppingId = 3, Name = "Phô mai viên", Price = 7000m, ImageUrl = "/Images/ToppingImages/phomaivien.jpg", Active = true },
                new Topping { ToppingId = 4, Name = "Khúc bạch chân mèo", Price = 7000m, ImageUrl = "/Images/ToppingImages/khucbachchanmeo.jpg", Active = true },
                new Topping { ToppingId = 5, Name = "Thạch khoai môn", Price = 6000m, ImageUrl = "/Images/ToppingImages/thachkhoaimon.jpg", Active = true },
                new Topping { ToppingId = 6, Name = "Bánh flan", Price = 6000m, ImageUrl = "/Images/ToppingImages/banhflan.jpg", Active = true }
            );
        }
    }

    // ========================== RECIPE ==========================
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> entity)
        {
            entity.ToTable("Recipes");

            entity.HasKey(x => x.RecipeId);

            entity.Property(x => x.Name)
                .HasMaxLength(200);

            entity.Property(x => x.YieldPercentage)
                .HasDefaultValue(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);


            entity.HasData(
                new Recipe { RecipeId = 1, Name = "Recipe CF Sữa", Active = true },
                new Recipe { RecipeId = 2, Name = "Recipe CF Đen", Active = true },
                new Recipe { RecipeId = 3, Name = "Recipe Trà sữa", Active = true },
                new Recipe { RecipeId = 4, Name = "Recipe Trà sữa socola", Active = true },
                new Recipe { RecipeId = 5, Name = "Trân châu đen", Active = true },
                new Recipe { RecipeId = 6, Name = "Trân châu trắng", Active = true }
            );
        }
    }

    // ========================== RECIPE DETAIL ==========================
    public class RecipeDetailConfiguration : IEntityTypeConfiguration<RecipeDetail>
    {
        public void Configure(EntityTypeBuilder<RecipeDetail> entity)
        {
            entity.ToTable("RecipeDetails", t =>
            {
                t.HasCheckConstraint(
                    "CK_RecipeDetail_OnlyOneSource",
                    @"(IngredientId IS NOT NULL AND ChildRecipeId IS NULL)
                    OR (IngredientId IS NULL AND ChildRecipeId IS NOT NULL)"
                );
            });

            entity.HasKey(x => x.RecipeDetailId);

            // ================= PROPERTIES =================
            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.UnitId)
                .IsRequired();

            // ================= RELATIONSHIPS =================

            entity.HasOne(x => x.Recipe)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ChildRecipe)
                .WithMany()
                .HasForeignKey(x => x.ChildRecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.RecipeId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL");

            entity.HasIndex(x => new { x.RecipeId, x.ChildRecipeId })
                .IsUnique()
                .HasFilter("[ChildRecipeId] IS NOT NULL");

            entity.HasIndex(x => x.UnitId); // 🔥 thêm để optimize

            // ================= SEED =================

            entity.HasData(
                // ===== Recipe 1 =====
                new RecipeDetail { RecipeDetailId = 1, RecipeId = 1, IngredientId = 1, Quantity = 50m, UnitId = 3 }, // ml
                new RecipeDetail { RecipeDetailId = 2, RecipeId = 1, IngredientId = 2, Quantity = 30m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 3, RecipeId = 1, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 2 =====
                new RecipeDetail { RecipeDetailId = 4, RecipeId = 2, IngredientId = 1, Quantity = 60m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 5, RecipeId = 2, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 3 =====
                new RecipeDetail { RecipeDetailId = 6, RecipeId = 3, IngredientId = 3, Quantity = 80m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 7, RecipeId = 3, IngredientId = 4, Quantity = 40m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 8, RecipeId = 3, IngredientId = 6, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 9, RecipeId = 3, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 4 =====
                new RecipeDetail { RecipeDetailId = 10, RecipeId = 4, IngredientId = 3, Quantity = 70m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 11, RecipeId = 4, IngredientId = 4, Quantity = 40m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 12, RecipeId = 4, IngredientId = 5, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 13, RecipeId = 4, IngredientId = 6, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 14, RecipeId = 4, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 5 (g) =====
                new RecipeDetail { RecipeDetailId = 15, RecipeId = 5, IngredientId = 11, Quantity = 100m, UnitId = 1 }, // g
                new RecipeDetail { RecipeDetailId = 16, RecipeId = 5, IngredientId = 12, Quantity = 50m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 17, RecipeId = 5, IngredientId = 13, Quantity = 60m, UnitId = 3 },

                // ===== Recipe 6 =====
                new RecipeDetail { RecipeDetailId = 18, RecipeId = 6, IngredientId = 11, Quantity = 100m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 19, RecipeId = 6, IngredientId = 6, Quantity = 40m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 20, RecipeId = 6, IngredientId = 13, Quantity = 60m, UnitId = 3 },

                // ===== Child recipe =====
                new RecipeDetail
                {
                    RecipeDetailId = 21,
                    RecipeId = 3,
                    ChildRecipeId = 5,
                    Quantity = 1,
                    UnitId = 1 // hoặc tạo unit "portion"
                }
            );
        }
    }
}