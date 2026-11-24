using Microsoft.EntityFrameworkCore;

namespace DesktopFlow.Infrastructure.Persistence;

public sealed class DesktopFlowDbContext : DbContext
{
    public DesktopFlowDbContext(DbContextOptions<DesktopFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<PreferenceEntity> Preferences => Set<PreferenceEntity>();

    public DbSet<RuleEntity> Rules => Set<RuleEntity>();

    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PreferenceEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Json).IsRequired();
        });

        modelBuilder.Entity<RuleEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Name).HasMaxLength(200);
            entity.HasIndex(row => row.Scope);
            entity.HasIndex(row => row.Priority);
        });

        modelBuilder.Entity<ActivityEntity>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.FileName).HasMaxLength(512);
            entity.Property(row => row.Kind).HasMaxLength(32);
            entity.Property(row => row.Status).HasMaxLength(32);
            entity.HasIndex(row => row.TimestampUtc);
            entity.HasIndex(row => row.FileName);
            entity.HasIndex(row => row.Status);
            entity.HasIndex(row => row.CategoryKey);
            entity.HasIndex(row => row.Kind);
        });
    }
}
