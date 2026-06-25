using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.Ratings
{
    public class RatingImageConfiguration : IEntityTypeConfiguration<RatingImage>
    {
        public void Configure(
            EntityTypeBuilder<RatingImage> entity)
        {
            entity.ToTable("RatingImages");

            entity.HasKey(x =>
                x.RatingImageId);

            // ================= IMAGE =================

            entity.Property(x =>
                x.ImageUrl)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(x =>
                x.PublicId)
                .IsRequired()
                .HasMaxLength(300);

            // ================= AUDIT =================

            entity.Property(x =>
                x.CreatedAt)
                .HasDefaultValueSql(
                    "GETDATE()");

            // ================= RELATIONSHIP =================

            entity.HasOne(x =>
                    x.Rating)
                .WithMany(x =>
                    x.Images)
                .HasForeignKey(x =>
                    x.RatingId)
                .OnDelete(
                    DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x =>
                x.RatingId);

            entity.HasIndex(x =>
                x.PublicId)
                .IsUnique();
        }
    }
}
