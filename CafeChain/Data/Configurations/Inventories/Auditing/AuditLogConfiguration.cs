using CafeChain.Models.Inventories.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Auditing
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> entity)
        {
            entity.ToTable("AuditLogs");

            entity.HasKey(x => x.AuditLogId);

            // ================= BASIC =================

            entity.Property(x => x.TableName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Action)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.OldData)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.NewData)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= INDEX =================

            entity.HasIndex(x => x.TableName);

            entity.HasIndex(x => x.RecordId);

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => x.CreatedAt);

            entity.HasIndex(x => new
            {
                x.TableName,
                x.RecordId
            });

            entity.HasIndex(x => new
            {
                x.TableName,
                x.Action
            });
        }
    }
}

