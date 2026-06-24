using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.DrinkInfos
{
    public class DrinkImageConfiguration : IEntityTypeConfiguration<DrinkImage>
    {
        public void Configure(EntityTypeBuilder<DrinkImage> entity)
        {
            entity.ToTable("DrinkImages");

            entity.HasKey(x =>
                x.DrinkImageId);

            // ================= IMAGE =================

            entity.Property(x =>
                x.ImageUrl)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(x =>
                x.PublicId)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(x =>
                x.IsDefault)
                .HasDefaultValue(false);

            // ================= AUDIT =================

            entity.Property(x =>
                x.CreatedAt)
                .HasDefaultValueSql(
                    "GETDATE()");

            // ================= RELATIONSHIP =================

            entity.HasOne(x =>
                    x.Drink)
                .WithMany(x =>
                    x.DrinkImages)
                .HasForeignKey(x =>
                    x.DrinkId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x =>
                x.DrinkId);

            entity.HasIndex(x =>
                new
                {
                    x.DrinkId,
                    x.IsDefault
                });

            // ================= SEED DATA ================= 
            entity.HasData(
                new DrinkImage { DrinkImageId = 1, DrinkId = 1, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803239/cps1_ip9ciu.jpg", PublicId = "cps1_ip9ciu", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 2, DrinkId = 1, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803240/cps2_zd0pyd.jpg", PublicId = "cps2_zd0pyd", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 3, DrinkId = 1, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803240/cps3_guo9om.jpg", PublicId = "cps3_guo9om", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 4, DrinkId = 1, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803241/cps4_koocly.jpg", PublicId = "cps4_koocly", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 5, DrinkId = 2, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803225/cpd1_cgkole.jpg", PublicId = "cpd1_cgkole", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 6, DrinkId = 2, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803236/cpd2_xgqlei.jpg", PublicId = "cpd2_xgqlei", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 7, DrinkId = 2, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803237/cpd3_dwyqpv.jpg", PublicId = "cpd3_dwyqpv", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 8, DrinkId = 2, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803238/cpd4_xphst1.jpg", PublicId = "cpd4_xphst1", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 9, DrinkId = 3, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803061/trasuatranchauden1_kekbpp.jpg", PublicId = "trasuatranchauden1_kekbpp", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 10, DrinkId = 3, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803062/trasuatranchauden2_m4kkru.jpg", PublicId = "trasuatranchauden2_m4kkru", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 11, DrinkId = 3, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803062/trasuatranchauden3_pcmlfn.jpg", PublicId = "trasuatranchauden3_pcmlfn", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 12, DrinkId = 3, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803063/trasuatranchauden4_cngwyr.jpg", PublicId = "trasuatranchauden4_cngwyr", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 13, DrinkId = 4, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802891/trasuasocola1_hc4t3p.jpg", PublicId = "trasuasocola1_hc4t3p", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 14, DrinkId = 4, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802891/trasuasocola2_m9yp1i.jpg", PublicId = "trasuasocola2_m9yp1i", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 15, DrinkId = 4, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802892/trasuasocola3_t8nr2b.jpg", PublicId = "trasuasocola3_t8nr2b", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 16, DrinkId = 4, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779802950/trasuasocola4_kju0s7.jpg", PublicId = "trasuasocola4_kju0s7", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 17, DrinkId = 5, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803393/sting1_tcita4.jpg", PublicId = "sting1_tcita4", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 18, DrinkId = 5, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803314/sting2_axipva.jpg", PublicId = "sting2_axipva", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 19, DrinkId = 5, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803314/sting3_rv03ev.jpg", PublicId = "sting3_rv03ev", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 20, DrinkId = 5, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803316/sting4_yzaesh.jpg", PublicId = "sting4_yzaesh", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 21, DrinkId = 6, IsDefault = true, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803080/coca1_qum0eb.jpg", PublicId = "coca1_qum0eb", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 22, DrinkId = 6, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803081/coca2_ctcrt0.jpg", PublicId = "coca2_ctcrt0", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 23, DrinkId = 6, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803082/coca3_mp28bz.jpg", PublicId = "coca3_mp28bz", CreatedAt = new DateTime(2025, 1, 1) },
                new DrinkImage { DrinkImageId = 24, DrinkId = 6, IsDefault = false, ImageUrl = "https://res.cloudinary.com/dzfizobk8/image/upload/v1779803082/coca4_xbh74i.jpg", PublicId = "coca4_xbh74i", CreatedAt = new DateTime(2025, 1, 1) }
            );
        }
    }
}
