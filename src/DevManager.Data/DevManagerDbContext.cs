using DevManager.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevManager.Data;

public class DevManagerDbContext(DbContextOptions<DevManagerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<DocSequence> DocSequences => Set<DocSequence>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.LoginId).IsUnique();
            e.Property(x => x.LoginId).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(50);
            e.HasOne(x => x.JobTitle).WithMany().HasForeignKey(x => x.JobTitleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<JobTitle>().HasIndex(x => x.Name).IsUnique();
        b.Entity<JobTitle>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Category>().HasIndex(x => x.Code).IsUnique();

        b.Entity<Project>().HasQueryFilter(x => x.DeletedAt == null);

        b.Entity<ProjectMember>(e =>
        {
            e.HasKey(x => new { x.ProjectId, x.UserId });
            e.HasOne(x => x.Project).WithMany(p => p.Members).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Requirement>(e =>
        {
            e.HasIndex(x => x.DocNo).IsUnique();
            e.HasIndex(x => x.AssigneeId);
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.Property(x => x.DocNo).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RequesterUser).WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Assignment>().HasIndex(x => x.RequirementId);
        b.Entity<DocSequence>().HasKey(x => x.Year);
        b.Entity<DocSequence>().Property(x => x.Year).ValueGeneratedNever();

        b.Entity<WorkTask>(e =>
        {
            e.ToTable("Tasks");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasIndex(x => x.AssigneeId);
            e.HasIndex(x => x.RequirementId);
        });

        b.Entity<Issue>(e =>
        {
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasIndex(x => x.AssigneeId);
        });

        b.Entity<Comment>().HasIndex(x => new { x.Entity, x.EntityId });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>().HasIndex(x => new { x.Entity, x.EntityId });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchRequirements();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchRequirements();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void TouchRequirements()
    {
        foreach (var entry in ChangeTracker.Entries<Requirement>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Version++;
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
