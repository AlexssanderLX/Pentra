using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Domain.Entities;

namespace Pentra.Infrastructure.Persistence;

public sealed class PentraDbContext : DbContext, IPentraDbContext
{
    public PentraDbContext(DbContextOptions<PentraDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ScopeTarget> Targets => Set<ScopeTarget>();
    public DbSet<PentestPhase> Phases => Set<PentestPhase>();
    public DbSet<SecurityTool> Tools => Set<SecurityTool>();
    public DbSet<ProjectToolSelection> ToolSelections => Set<ProjectToolSelection>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<ReportDraft> Reports => Set<ReportDraft>();
    public DbSet<ChangeHistoryEntry> History => Set<ChangeHistoryEntry>();
    public DbSet<ToolRun> ToolRuns => Set<ToolRun>();
    public DbSet<ToolRunLogLine> ToolRunLogs => Set<ToolRunLogLine>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Client).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(4000);
            e.Property(p => p.EngagementNotes).HasMaxLength(8000);
            e.Property(p => p.Status).HasConversion<int>();
            e.HasIndex(p => p.Status);

            e.HasMany(p => p.Targets).WithOne(t => t.Project!).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.ToolSelections).WithOne(s => s.Project!).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Evidence).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Reports).WithOne(r => r.Project!).HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.History).WithOne(h => h.Project!).HasForeignKey(h => h.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Runs).WithOne(r => r.Project!).HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ScopeTarget>(e =>
        {
            e.Property(t => t.Value).IsRequired().HasMaxLength(500);
            e.Property(t => t.Kind).HasConversion<int>();
            e.Property(t => t.Notes).HasMaxLength(2000);
            e.HasIndex(t => new { t.ProjectId, t.Value }).IsUnique();
        });

        builder.Entity<PentestPhase>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(150);
            e.Property(p => p.Slug).IsRequired().HasMaxLength(100);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasIndex(p => p.Order).IsUnique();
        });

        builder.Entity<SecurityTool>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(150);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.Command).HasMaxLength(100);
            e.Property(t => t.ReferenceUrl).HasMaxLength(500);
            e.Property(t => t.Category).HasConversion<int>();
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasOne(t => t.DefaultPhase).WithMany().HasForeignKey(t => t.DefaultPhaseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectToolSelection>(e =>
        {
            e.Property(s => s.Notes).HasMaxLength(2000);
            e.HasIndex(s => new { s.ProjectId, s.PhaseId, s.SecurityToolId }).IsUnique();
            e.HasOne(s => s.Phase).WithMany(p => p.ToolSelections).HasForeignKey(s => s.PhaseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.SecurityTool).WithMany(t => t.Selections).HasForeignKey(s => s.SecurityToolId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Evidence>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.HasOne(x => x.Phase).WithMany().HasForeignKey(x => x.PhaseId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ReportDraft>(e =>
        {
            e.Property(r => r.Title).IsRequired().HasMaxLength(250);
            e.Property(r => r.Summary).HasMaxLength(8000);
        });

        builder.Entity<ChangeHistoryEntry>(e =>
        {
            e.Property(h => h.EntityType).IsRequired().HasMaxLength(100);
            e.Property(h => h.Summary).IsRequired().HasMaxLength(1000);
            e.Property(h => h.Action).HasConversion<int>();
            e.HasIndex(h => new { h.ProjectId, h.CreatedAt });
        });

        builder.Entity<ToolRun>(e =>
        {
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.ToolSlug).IsRequired().HasMaxLength(100);
            e.Property(r => r.ImageRef).HasMaxLength(300);
            e.Property(r => r.TargetValue).HasMaxLength(500);
            e.Property(r => r.ParametersJson).HasMaxLength(8000);
            e.Property(r => r.ApprovedArgumentsJson).HasMaxLength(8000);
            e.Property(r => r.AuthorizationReason).HasMaxLength(1000);
            e.Property(r => r.FailureReason).HasMaxLength(2000);
            e.Property(r => r.ArtifactSha256).HasMaxLength(64);
            e.Property(r => r.RunnerId).HasMaxLength(100);
            // Result/output columns can be large; no length cap (application truncates).

            e.HasIndex(r => new { r.Status, r.CreatedAt });
            e.HasIndex(r => new { r.ProjectId, r.CreatedAt });

            e.HasOne(r => r.Phase).WithMany().HasForeignKey(r => r.PhaseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.SecurityTool).WithMany().HasForeignKey(r => r.SecurityToolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ScopeTarget).WithMany().HasForeignKey(r => r.ScopeTargetId).OnDelete(DeleteBehavior.SetNull);

            e.HasMany(r => r.Logs).WithOne(l => l.ToolRun!).HasForeignKey(l => l.ToolRunId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ToolRunLogLine>(e =>
        {
            e.Property(l => l.Stream).HasConversion<int>();
            e.Property(l => l.Text).HasMaxLength(4000);
            e.HasIndex(l => new { l.ToolRunId, l.Seq }).IsUnique();
        });
    }
}
