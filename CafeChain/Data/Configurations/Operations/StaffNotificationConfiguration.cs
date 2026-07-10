using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Operations
{
    public class StaffNotificationConfiguration : IEntityTypeConfiguration<StaffNotification>
    {
        public void Configure(EntityTypeBuilder<StaffNotification> entity)
        {
            entity.ToTable("StaffNotifications");

            entity.HasKey(x => x.StaffNotificationId);

            entity.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Body)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(x => x.EntityType)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.EmailErrorSummary)
                .HasMaxLength(500);

            entity.Property(x => x.IsRead)
                .HasDefaultValue(false);

            entity.Property(x => x.EmailAttempted)
                .HasDefaultValue(false);

            entity.Property(x => x.EmailSent)
                .HasDefaultValue(false);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RecipientStaff)
                .WithMany()
                .HasForeignKey(x => x.RecipientStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.RecipientStaffId, x.IsRead })
                .HasDatabaseName("IX_StaffNotification_Recipient_IsRead");

            entity.HasIndex(x => x.StoreId)
                .HasDatabaseName("IX_StaffNotification_StoreId");

            entity.HasIndex(x => new { x.EntityType, x.EntityId })
                .HasDatabaseName("IX_StaffNotification_Entity");
        }
    }
}