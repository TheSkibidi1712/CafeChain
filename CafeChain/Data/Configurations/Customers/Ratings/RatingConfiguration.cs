using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.Ratings
{
    public class RatingConfiguration : IEntityTypeConfiguration<Rating>
    {
        public void Configure(EntityTypeBuilder<Rating> entity)
        {
            entity.ToTable("Ratings", t =>
            {
                t.HasCheckConstraint("CK_Rating_Stars", "[Stars] BETWEEN 1 AND 5");
            });

            entity.HasKey(x => x.RatingId);

            entity.Property(x => x.Stars)
                .IsRequired();

            entity.Property(x => x.Comment)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Drink)
                .WithMany(d => d.Ratings)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 SELF RELATION (reply comment)
            entity.HasOne(x => x.ParentRating)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentRatingId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            // 🔥 chỉ cho phép 1 user rating 1 drink (chỉ áp dụng cho comment CHA)
            entity.HasIndex(x => new { x.CustomerId, x.DrinkId })
                .IsUnique()
                .HasFilter("[ParentRatingId] IS NULL AND [CustomerId] IS NOT NULL AND [DrinkId] IS NOT NULL");

            // 🔥 index để load comment nhanh
            entity.HasIndex(x => x.DrinkId);
            entity.HasIndex(x => x.ParentRatingId);
        }
    }
}
