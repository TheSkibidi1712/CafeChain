using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Debts
{
    public class InventoryDebtConfiguration : IEntityTypeConfiguration<InventoryDebt>
    {
        public void Configure(EntityTypeBuilder<InventoryDebt> builder)
        {
            builder.ToTable("InventoryDebts");

            // ================= PRIMARY KEY =================
            builder.HasKey(x => x.InventoryDebtId);

            // ================= PROPERTIES =================
            builder.Property(x => x.PartnerName)
                .HasMaxLength(255);

            builder.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(x => x.PaidAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.PaidAt)
                .IsRequired(false);

            // ================= INDEX =================
            builder.HasIndex(x => x.InventoryDocumentId);

            builder.HasIndex(x => new { x.PartnerType, x.PartnerId });
            builder.HasIndex(x => x.PaidAmount);

            // ================= RELATION =================
            builder.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.Debts)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= CONSTRAINT =================

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InventoryDebt_Amount", "[Amount] >= 0");
                t.HasCheckConstraint("CK_InventoryDebt_PaidAmount", "[PaidAmount] >= 0");
                t.HasCheckConstraint("CK_InventoryDebt_Paid_Limit", "[PaidAmount] <= [Amount]");
            });
        }
    }
}
