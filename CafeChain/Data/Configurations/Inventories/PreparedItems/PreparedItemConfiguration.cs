using CafeChain.Models.Inventories.PreparedItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.PreparedItems
{
    public class PreparedItemConfiguration : IEntityTypeConfiguration<PreparedItem>
    {
        public void Configure(EntityTypeBuilder<PreparedItem> entity)
        {
            entity.ToTable("PreparedItems");

            entity.HasKey(x => x.PreparedItemId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.Active)
                .HasDefaultValue(true)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.BaseUnitId);

            entity.HasIndex(x => x.Active);

            entity.HasOne(x => x.BaseUnit)
                .WithMany()
                .HasForeignKey(x => x.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // No seeds in #116 — explicit mapping is later issues.
        }
    }
}
