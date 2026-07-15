using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierStoreConfiguration : IEntityTypeConfiguration<SupplierStore>
    {
        public void Configure(EntityTypeBuilder<SupplierStore> entity)
        {
            entity.ToTable("SupplierStores", table =>
            {
                table.HasCheckConstraint(
                    "CK_SupplierStore_LeadTimeOverride",
                    "[LeadTimeOverrideDays] IS NULL OR [LeadTimeOverrideDays] >= 0");
            });

            entity.HasKey(x => x.SupplierStoreId);
            entity.Property(x => x.Active).HasDefaultValue(true);
            entity.Property(x => x.DeliverySchedule).HasMaxLength(300);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.SupplierId, x.StoreId }).IsUnique();
            entity.HasIndex(x => new { x.StoreId, x.Active });

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.SupplierStores)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
