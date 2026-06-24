using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.Ratings
{
    public class RatingReactionConfiguration : IEntityTypeConfiguration<RatingReaction>
    {
        public void Configure(EntityTypeBuilder<RatingReaction> entity)
        {
            entity.ToTable("RatingReactions");

            entity.HasKey(x => x.RatingReactionId);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne(x => x.Rating)
                .WithMany(x => x.Reactions)
                .HasForeignKey(x => x.RatingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= UNIQUE =================

            // 🔥 mỗi user chỉ 1 reaction / 1 comment
            entity.HasIndex(x => new { x.RatingId, x.CustomerId })
                .IsUnique();

            // ================= INDEX =================

            entity.HasIndex(x => x.RatingId);
        }
    }
}
