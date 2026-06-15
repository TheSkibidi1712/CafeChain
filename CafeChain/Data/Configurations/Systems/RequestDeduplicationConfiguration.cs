using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Systems
{
    public class RequestDeduplicationConfiguration : IEntityTypeConfiguration<RequestDeduplication>
    {
        public void Configure(EntityTypeBuilder<RequestDeduplication> entity)
        {
            entity.ToTable("RequestDeduplications", t =>
            {
                t.HasCheckConstraint(
                    "CK_RequestDeduplication_ExpiredAt",
                    "[ExpiredAt] > [CreatedAt]"
                );

                t.HasCheckConstraint(
                    "CK_RequestDeduplication_Status",
                    "[Status] IN ('PENDING', 'SUCCESS', 'FAILED')"
                );
            });

            entity.HasKey(x => x.RequestDeduplicationId);

            // ================= PROPERTY =================

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

            entity.Property(x => x.ResponseBody)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            entity.Property(x => x.ExpiredAt)
                .IsRequired();

            // ================= INDEX =================

            // mỗi request key chỉ tồn tại 1 lần
            entity.HasIndex(x => x.RequestKey)
                .IsUnique();

            // query theo action
            entity.HasIndex(x => x.ActionName);

            // cleanup expired records
            entity.HasIndex(x => x.ExpiredAt);

            // query staff activity
            entity.HasIndex(x => x.StaffId);

            // query trạng thái
            entity.HasIndex(x => x.Status);

            // query nhanh request theo action + staff
            entity.HasIndex(x => new
            {
                x.ActionName,
                x.StaffId
            });

            // query retry logic
            entity.HasIndex(x => new
            {
                x.Status,
                x.ExpiredAt
            });


        }
    }
}
