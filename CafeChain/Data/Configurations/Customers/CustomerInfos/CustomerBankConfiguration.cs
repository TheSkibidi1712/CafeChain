using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Customers.CustomerInfos
{
    public class CustomerBankConfiguration : IEntityTypeConfiguration<CustomerBank>
    {
        public void Configure(EntityTypeBuilder<CustomerBank> entity)
        {
            entity.ToTable("CustomerBanks");

            entity.HasKey(x => x.CustomerBankId);

            entity.Property(x => x.BankName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.AccountNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => new { x.BankName, x.AccountNumber })
                .IsUnique();

            // ===== CLEAN SLATE: Chỉ 1 Bank cho Customer 111 =====
            entity.HasData(
                new CustomerBank { CustomerBankId = 1, CustomerId = 111, BankName = "Vietcombank", AccountNumber = "111222333444" }
            );
        }
    }
}
