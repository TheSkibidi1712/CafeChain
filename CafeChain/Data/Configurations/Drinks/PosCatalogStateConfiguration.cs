using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks
{
    public class PosCatalogStateConfiguration : IEntityTypeConfiguration<PosCatalogState>
    {
        public void Configure(EntityTypeBuilder<PosCatalogState> entity)
        {
            entity.ToTable("PosCatalogStates");
            entity.HasKey(x => x.PosCatalogStateId);
            entity.Property(x => x.Version).HasDefaultValue(0L);
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(x => x.Store).WithOne(x => x.PosCatalogState)
                .HasForeignKey<PosCatalogState>(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.StoreId).IsUnique();
        }
    }
}
