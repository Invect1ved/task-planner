using Microsoft.EntityFrameworkCore;
using TaskPlanner.Data.Entities;

namespace TaskPlanner.Data;

public class PlannerDbContext : DbContext
{
    public PlannerDbContext(DbContextOptions<PlannerDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PlannerTask> Tasks => Set<PlannerTask>();
    public DbSet<TaskShare> Shares => Set<TaskShare>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_user");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(120);
            entity.Property(e => e.DisplayName).HasMaxLength(120);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<PlannerTask>(entity =>
        {
            entity.ToTable("task");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.DueAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamp without time zone");
            entity.HasOne(e => e.Owner).WithMany(e => e.OwnedTasks).HasForeignKey(e => e.OwnerId);
        });

        modelBuilder.Entity<TaskShare>(entity =>
        {
            entity.ToTable("task_share");
            entity.HasKey(e => new { e.TaskId, e.UserId });
            entity.HasOne(e => e.Task).WithMany(e => e.Shares).HasForeignKey(e => e.TaskId);
            entity.HasOne(e => e.User).WithMany(e => e.Shares).HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("refresh_session");
            entity.Property(e => e.ExpiresAt).HasColumnType("timestamp without time zone");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });
    }
}
