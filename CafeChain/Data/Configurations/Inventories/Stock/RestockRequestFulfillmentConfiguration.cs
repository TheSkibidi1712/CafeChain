using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class RestockRequestFulfillmentConfiguration : IEntityTypeConfiguration<RestockRequestFulfillment>
    {
        public void Configure(EntityTypeBuilder<RestockRequestFulfillment> entity)
        {
            entity.ToTable("RestockRequestFulfillments");

            entity.HasKey(x => x.RestockRequestFulfillmentId);

            entity.Property(x => x.SourceType)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.PlannedBaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt).IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.RestockRequest)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.RestockRequestId);
            entity.HasIndex(x => x.SourceType);
            entity.HasIndex(x => x.Status);
        }
    }
}
