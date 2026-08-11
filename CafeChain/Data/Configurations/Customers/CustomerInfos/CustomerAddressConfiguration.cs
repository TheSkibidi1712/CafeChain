using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.CustomerInfos
{
    public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
    {
        public void Configure(EntityTypeBuilder<CustomerAddress> entity)
        {
            entity.ToTable("CustomerAddresses");

            entity.HasKey(x => x.CustomerAddressId);

            entity.Property(x => x.Address)
                .IsRequired()
                .HasMaxLength(300);

            // ─── GPS coordinates precision ────────────────────────────────────────
            entity.Property(x => x.Latitude)
                .HasColumnType("decimal(9,6)");

            entity.Property(x => x.Longitude)
                .HasColumnType("decimal(9,6)");

            // ─── Location FK relationships (all Restrict — địa chỉ không bị xóa khi xóa Phường/Quận/Tỉnh) ───
            entity.HasOne(x => x.Ward)
                .WithMany(w => w.CustomerAddresses)
                .HasForeignKey(x => x.WardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
