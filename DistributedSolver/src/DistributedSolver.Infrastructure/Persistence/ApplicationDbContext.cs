using DistributedSolver.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DistributedSolver.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskModel> Tasks { get; set; } = default!;
    public DbSet<UserModel> Users { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Зв’язок: User 1..∞ Tasks
        modelBuilder.Entity<TaskModel>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tasks)
            .HasForeignKey(t => t.UserId);

        // Конфігурація та індекси для TaskModel
        modelBuilder.Entity<TaskModel>(entity =>
        {
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_tasks_pending")
                .HasFilter($@"""{nameof(TaskModel.Status)}"" = 'PENDING'");

            entity.Property(e => e.Status)
                .HasMaxLength(20);

            entity.Property(e => e.TimeCreated)
                .HasColumnType("timestamp without time zone");
        });

        // Email повинен бути унікальним
        modelBuilder.Entity<UserModel>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}