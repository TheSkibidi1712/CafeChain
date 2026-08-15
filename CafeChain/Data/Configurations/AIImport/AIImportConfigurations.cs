using CafeChain.Models.AIImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.AIImport;

public sealed class ImportSessionConfiguration : IEntityTypeConfiguration<ImportSession>
{
    public void Configure(EntityTypeBuilder<ImportSession> entity)
    {
        entity.ToTable("ImportSessions", table => table.HasCheckConstraint(
            "CK_ImportSessions_Status",
            "[Status] IN ('UPLOADED','ANALYZING','VALIDATING','READY_TO_PREVIEW','IMPORTING','COMPLETED','FAILED','CANCELLED','EXPIRED')"));
        entity.HasKey(x => x.ImportSessionId);
        entity.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        entity.Property(x => x.FileHash).IsRequired().HasMaxLength(64);
        entity.Property(x => x.SourceFormat).IsRequired().HasMaxLength(10);
        entity.Property(x => x.SourceMetadataJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceSnapshotJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
        entity.Property(x => x.ModelName).HasMaxLength(150);
        entity.Property(x => x.PromptVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.FailureCode).HasMaxLength(100);
        entity.Property(x => x.FailureMessage).HasMaxLength(1000);
        entity.Property(x => x.AnalysisWarningsJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.UploadedByAccountId, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        entity.HasIndex(x => x.FileHash);
    }
}

public sealed class ImportGroupConfiguration : IEntityTypeConfiguration<ImportGroup>
{
    public void Configure(EntityTypeBuilder<ImportGroup> entity)
    {
        entity.ToTable("ImportGroups");
        entity.HasKey(x => x.ImportGroupId);
        entity.Property(x => x.SheetName).IsRequired().HasMaxLength(150);
        entity.Property(x => x.RegionAddress).IsRequired().HasMaxLength(50);
        entity.Property(x => x.SourceLabel).IsRequired().HasMaxLength(200);
        entity.Property(x => x.SourceLocatorJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.ExtractionMode).IsRequired().HasMaxLength(50);
        entity.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.MappingJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceHeadersJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceColumnsJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.IssuesJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.Confidence).HasPrecision(5, 4);
        entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
        entity.HasIndex(x => new { x.ImportSessionId, x.SheetName });
        entity.HasOne(x => x.Session).WithMany(x => x.Groups).HasForeignKey(x => x.ImportSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportItemConfiguration : IEntityTypeConfiguration<ImportItem>
{
    public void Configure(EntityTypeBuilder<ImportItem> entity)
    {
        entity.ToTable("ImportItems", table =>
        {
            table.HasCheckConstraint("CK_ImportItems_Action", "[Action] IN ('CREATE','SKIP')");
            table.HasCheckConstraint("CK_ImportItems_Status", "[Status] IN ('VALID','WARNING','ERROR','REVIEW_REQUIRED','SKIPPED','IMPORTED')");
        });
        entity.HasKey(x => x.ImportItemId);
        entity.Property(x => x.RawDataJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.NormalizedDataJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceTraceJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceLocatorJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.EvidenceSnippet).HasMaxLength(4000);
        entity.Property(x => x.ErrorsJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.WarningsJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.SourceIssuesJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
        entity.Property(x => x.Action).IsRequired().HasMaxLength(20);
        entity.Property(x => x.Confidence).HasPrecision(5, 4);
        entity.Property(x => x.AiConfidence).HasPrecision(5, 4);
        entity.Property(x => x.OcrConfidence).HasPrecision(5, 4);
        entity.Property(x => x.DuplicateOverrideReason).HasMaxLength(500);
        entity.Property(x => x.ManualReviewPayloadHash).HasMaxLength(64);
        entity.HasIndex(x => new { x.ImportGroupId, x.SourceRow });
        entity.HasIndex(x => new { x.Status, x.Action });
        entity.HasOne(x => x.Group).WithMany(x => x.Items).HasForeignKey(x => x.ImportGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportAuditConfiguration : IEntityTypeConfiguration<ImportAudit>
{
    public void Configure(EntityTypeBuilder<ImportAudit> entity)
    {
        entity.ToTable("ImportAudits");
        entity.HasKey(x => x.ImportAuditId);
        entity.Property(x => x.Action).IsRequired().HasMaxLength(100);
        entity.Property(x => x.StatusBefore).HasMaxLength(30);
        entity.Property(x => x.StatusAfter).HasMaxLength(30);
        entity.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.ModelName).HasMaxLength(150);
        entity.Property(x => x.PromptVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.IdempotencyKeyHash).HasMaxLength(64);
        entity.Property(x => x.ResultSummaryJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.ErrorCode).HasMaxLength(100);
        entity.Property(x => x.SourceFormat).IsRequired().HasMaxLength(10);
        entity.Property(x => x.ExtractionMode).HasMaxLength(200);
        entity.HasIndex(x => new { x.ImportSessionId, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.StaffId, x.CreatedAtUtc });
        entity.HasOne(x => x.Session).WithMany(x => x.Audits).HasForeignKey(x => x.ImportSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
