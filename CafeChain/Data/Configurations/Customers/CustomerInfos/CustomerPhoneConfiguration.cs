using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.CustomerInfos
{
    public class CustomerPhoneConfiguration : IEntityTypeConfiguration<CustomerPhone>
    {
        public void Configure(EntityTypeBuilder<CustomerPhone> entity)
        {
            entity.ToTable("CustomerPhones");

            entity.HasKey(x => x.CustomerPhoneId);

            entity.Property(x => x.Phone)
                .IsRequired()
                .HasMaxLength(15);

            entity.HasIndex(x => new { x.Phone })
                .IsUnique();

            // ===== CLEAN SLATE: Chỉ 1 Phone cho Customer 111 =====
            entity.HasData(
                new CustomerPhone { CustomerPhoneId = 1, CustomerId = 111, Phone = "0900111222" }
            );
        }
    }
}
