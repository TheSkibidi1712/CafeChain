using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Toppings
{
    public class ToppingConfiguration : IEntityTypeConfiguration<Topping>
    {
        public void Configure(EntityTypeBuilder<Topping> entity)
        {
            entity.ToTable("Toppings");

            entity.HasKey(x =>
                x.ToppingId);

            // ================= PROPERTIES =================

            entity.Property(x =>
                x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.ToppingCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x =>
                x.Price)
                .HasColumnType(
                    "decimal(18,2)")
                .IsRequired();

            entity.Property(x =>
                x.ImageUrl)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(x =>
                x.ImagePublicId)
                .HasMaxLength(300)
                .IsRequired(false);

            entity.Property(x =>
                x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasIndex(x => x.ToppingCode)
                .IsUnique();

            // ================= RELATIONSHIPS =================

            entity.HasMany(x =>
                    x.DrinkToppings)
                .WithOne(x =>
                    x.Topping)
                .HasForeignKey(x =>
                    x.ToppingId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            entity.HasMany(x =>
                    x.StoreToppings)
                .WithOne(x =>
                    x.Topping)
                .HasForeignKey(x =>
                    x.ToppingId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            entity.HasMany(x =>
                    x.OrderToppings)
                .WithOne(x =>
                    x.Topping)
                .HasForeignKey(x =>
                    x.ToppingId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            // ================= SEED DATA =================
            entity.HasData(
                new Topping { ToppingId = 1, Name = "Trân châu đen", ToppingCode = "TC_DEN", Price = 5000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804079/tranchauden_ftddpx.jpg", ImagePublicId = "tranchauden_ftddpx", Active = true },
                new Topping { ToppingId = 2, Name = "Trân châu trắng", ToppingCode = "TC_TRANG", Price = 5000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804079/tranchautrang_c2pylw.jpg", ImagePublicId = "tranchautrang_c2pylw", Active = true },
                new Topping { ToppingId = 3, Name = "Phô mai viên", ToppingCode = "PM_VIEN", Price = 7000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804075/phomaivien_ujfenk.jpg", ImagePublicId = "phomaivien_ujfenk", Active = true },
                new Topping { ToppingId = 4, Name = "Khúc bạch chân mèo", ToppingCode = "KB_CM", Price = 7000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804082/khucbachchanmeo_r2fxzd.jpg", ImagePublicId = "khucbachchanmeo_r2fxzd", Active = true },
                new Topping { ToppingId = 5, Name = "Thạch khoai môn", ToppingCode = "TH_KM", Price = 6000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804078/thachkhoaimon_fwpprq.jpg", ImagePublicId = "thachkhoaimon_fwpprq", Active = true },
                new Topping { ToppingId = 6, Name = "Bánh flan", ToppingCode = "BH_FLAN", Price = 6000m, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779804080/banhflan_zndwvl.jpg", ImagePublicId = "banhflan_zndwvl", Active = true }
            );
        }
    }
}
