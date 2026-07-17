using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierDuplicateWarningConfiguration : IEntityTypeConfiguration<SupplierDuplicateWarning>
    {
        public void Configure(EntityTypeBuilder<SupplierDuplicateWarning> entity)
        {
            entity.ToTable("SupplierDuplicateWarnings");
            entity.HasKey(x => x.SupplierDuplicateWarningId);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entity.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.WarningFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.MatchedSupplierIdsJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(x => x.MatchedSignalsJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(x => x.OverrideReason).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.RequestedByStaffId, x.Status, x.ExpiresAtUtc });
            entity.HasIndex(x => x.CreatedSupplierId);

            entity.HasOne<Staff>()
                .WithMany()
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(x => x.CreatedSupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
