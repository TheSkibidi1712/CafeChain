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
            entity.Property(x => x.PosCatalogStateId).ValueGeneratedNever();
            entity.Property(x => x.Version).HasDefaultValue(0L);
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.HasData(new PosCatalogState
            {
                PosCatalogStateId = 1,
                Version = 0,
                UpdatedAtUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
    }
}
