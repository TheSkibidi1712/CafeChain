using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Systems
{
    public class RequestDeduplicationConfiguration : IEntityTypeConfiguration<RequestDeduplication>
    {
        public void Configure(EntityTypeBuilder<RequestDeduplication> entity)
        {
            entity.ToTable("RequestDeduplications", table =>
            {
                table.HasCheckConstraint(
                    "CK_RequestDeduplication_ExpiredAt",
                    "[ExpiredAt] > [CreatedAt]");

                table.HasCheckConstraint(
                    "CK_RequestDeduplication_Status",
                    "[Status] IN ('PROCESSING', 'SUCCESS', 'FAILED', 'EXPIRED')");
            });

            entity.HasKey(x => x.RequestDeduplicationId);

            entity.Property(x => x.RequestKey)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.ActionName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.RequestBody)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.PayloadHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.ResponseBody)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            entity.Property(x => x.ExpiredAt)
                .IsRequired();

            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.RequestKey, x.ActionName, x.StaffId, x.StoreId })
                .IsUnique();

            entity.HasIndex(x => x.ActionName);
            entity.HasIndex(x => x.ExpiredAt);
            entity.HasIndex(x => x.StaffId);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.ActionName, x.StaffId });
            entity.HasIndex(x => new { x.Status, x.ExpiredAt });
        }
    }
}
