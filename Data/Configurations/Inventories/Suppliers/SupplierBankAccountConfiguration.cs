using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierBankAccountConfiguration : IEntityTypeConfiguration<SupplierBankAccount>
    {
        public void Configure(EntityTypeBuilder<SupplierBankAccount> entity)
        {
            entity.ToTable("SupplierBankAccounts");

            entity.HasKey(x => x.SupplierBankAccountId);

            // ================= PROPERTY =================

            entity.Property(x => x.BankName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.AccountNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.AccountHolder)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Branch)
                .HasMaxLength(150);

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => new
            {
                x.SupplierId,
                x.AccountNumber
            }).IsUnique();

            // ================= RELATION =================

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.BankAccounts)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(
                new SupplierBankAccount
                {
                    SupplierBankAccountId = 1,
                    SupplierId = 1,
                    BankName = "Vietcombank",
                    AccountNumber = "111111111",
                    AccountHolder = "NCC A",
                    Branch = "Bình Dương",
                    IsPrimary = true,
                    Active = true
                },
                new SupplierBankAccount
                {
                    SupplierBankAccountId = 2,
                    SupplierId = 2,
                    BankName = "ACB",
                    AccountNumber = "222222222",
                    AccountHolder = "NCC B",
                    Branch = "TP HCM",
                    IsPrimary = true,
                    Active = true
                },
                new SupplierBankAccount
                {
                    SupplierBankAccountId = 3,
                    SupplierId = 3,
                    BankName = "Techcombank",
                    AccountNumber = "333333333",
                    AccountHolder = "NCC C",
                    Branch = "Đồng Nai",
                    IsPrimary = true,
                    Active = true
                },
                new SupplierBankAccount
                {
                    SupplierBankAccountId = 4,
                    SupplierId = 4,
                    BankName = "BIDV",
                    AccountNumber = "444444444",
                    AccountHolder = "NCC D",
                    Branch = "Hà Nội",
                    IsPrimary = true,
                    Active = true
                },
                new SupplierBankAccount
                {
                    SupplierBankAccountId = 5,
                    SupplierId = 5,
                    BankName = "MB Bank",
                    AccountNumber = "555555555",
                    AccountHolder = "NCC E",
                    Branch = "Đà Nẵng",
                    IsPrimary = true,
                    Active = true
                }
            );
        }
    }
}
