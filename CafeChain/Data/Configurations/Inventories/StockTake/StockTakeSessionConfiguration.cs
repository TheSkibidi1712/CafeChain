using CafeChain.Models.Inventories.StockTake;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.StockTake
{
    public class StockTakeSessionConfiguration : IEntityTypeConfiguration<StockTakeSession>
    {
        public void Configure(EntityTypeBuilder<StockTakeSession> entity)
        {
            entity.ToTable("StockTakeSessions");

            entity.HasKey(x => x.StockTakeSessionId);

            // ================= PROPERTY =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne<Store>()
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Staff>()
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Details)
                .WithOne(x => x.StockTakeSession)
                .HasForeignKey(x => x.StockTakeSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.StoreId);

            entity.HasIndex(x => x.StaffId);

            entity.HasIndex(x => x.CreatedAt);
        }
    }
}

