using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Systems;

public sealed class DocumentNumberCounterConfiguration : IEntityTypeConfiguration<DocumentNumberCounter>
{
    public void Configure(EntityTypeBuilder<DocumentNumberCounter> entity)
    {
        entity.ToTable("DocumentNumberCounters", table => table.HasCheckConstraint(
            "CK_DocumentNumberCounter_Value", "[LastValue] > 0"));
        entity.HasKey(x => x.DocumentNumberCounterId);
        entity.Property(x => x.CounterKey).HasMaxLength(100).IsRequired();
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.CounterKey, x.DateKey }).IsUnique();
    }
}
