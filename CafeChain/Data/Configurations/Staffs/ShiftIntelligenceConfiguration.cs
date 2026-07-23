using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Staffs;

public abstract class RowVersionConfiguration<T> where T : class
{
    protected static void Version(EntityTypeBuilder<T> b, string property) => b.Property<byte[]>(property).IsRowVersion();
}

public sealed class StaffAvailabilityRuleConfiguration : IEntityTypeConfiguration<StaffAvailabilityRule>
{
    public void Configure(EntityTypeBuilder<StaffAvailabilityRule> b) { b.HasKey(x => x.StaffAvailabilityRuleId); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StaffId, x.DayOfWeek, x.EffectiveFrom }); b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class StaffAvailabilityExceptionConfiguration : IEntityTypeConfiguration<StaffAvailabilityException>
{
    public void Configure(EntityTypeBuilder<StaffAvailabilityException> b) { b.HasKey(x => x.StaffAvailabilityExceptionId); b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StaffId, x.Date }); b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class StaffTimeOffConfiguration : IEntityTypeConfiguration<StaffTimeOff>
{
    public void Configure(EntityTypeBuilder<StaffTimeOff> b) { b.HasKey(x => x.StaffTimeOffId); b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StaffId, x.FromUtc, x.ToUtc }); b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.RequestedByStaff).WithMany().HasForeignKey(x => x.RequestedByStaffId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ReviewedByStaff).WithMany().HasForeignKey(x => x.ReviewedByStaffId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class StaffWorkConstraintConfiguration : IEntityTypeConfiguration<StaffWorkConstraint>
{
    public void Configure(EntityTypeBuilder<StaffWorkConstraint> b) { b.HasKey(x => x.StaffWorkConstraintId); b.Property(x => x.TargetWeeklyHours).HasPrecision(6,2); b.Property(x => x.MaxWeeklyHours).HasPrecision(6,2); b.Property(x => x.MaxDailyHours).HasPrecision(6,2); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StaffId, x.EffectiveFrom }); b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class StoreStaffingRequirementConfiguration : IEntityTypeConfiguration<StoreStaffingRequirement>
{
    public void Configure(EntityTypeBuilder<StoreStaffingRequirement> b) { b.HasKey(x => x.StoreStaffingRequirementId); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StoreId, x.ShiftId, x.DayOfWeek, x.EffectiveFrom }); b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.RequiredRole).WithMany().HasForeignKey(x => x.RequiredRoleId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class ScheduleOptimizationProposalConfiguration : IEntityTypeConfiguration<ScheduleOptimizationProposal>
{
    public void Configure(EntityTypeBuilder<ScheduleOptimizationProposal> b) { b.HasKey(x => x.ScheduleOptimizationProposalId); b.Property(x => x.ConstraintVersion).HasMaxLength(40); b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.ScoreBreakdownJson).HasMaxLength(2000); b.Property(x => x.ViolationsJson).HasMaxLength(4000); b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.StoreId, x.FromDate, x.ToDate, x.CreatedAtUtc }); b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ForecastRun).WithMany().HasForeignKey(x => x.ForecastRunId).OnDelete(DeleteBehavior.SetNull); }
}
public sealed class ScheduleOptimizationAssignmentConfiguration : IEntityTypeConfiguration<ScheduleOptimizationAssignment>
{
    public void Configure(EntityTypeBuilder<ScheduleOptimizationAssignment> b) { b.HasKey(x => x.ScheduleOptimizationAssignmentId); b.Property(x => x.ReasonCodesJson).HasMaxLength(1000); b.HasIndex(x => new { x.ScheduleOptimizationProposalId, x.StaffId, x.ShiftId, x.WorkDate }).IsUnique(); b.HasOne(x => x.Proposal).WithMany(x => x.Assignments).HasForeignKey(x => x.ScheduleOptimizationProposalId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict); }
}
