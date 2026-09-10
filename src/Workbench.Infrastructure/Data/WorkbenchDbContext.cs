using Microsoft.EntityFrameworkCore;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Infrastructure.Data;

public class WorkbenchDbContext : DbContext, IUnitOfWork
{
    public WorkbenchDbContext(DbContextOptions<WorkbenchDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Issue> Issues => Set<Issue>();

    public DbSet<Page> Pages => Set<Page>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 설정 클래스를 파일 단위로 분리해 두고 어셈블리 스캔으로 등록한다 —
        // 엔티티가 늘어날 때 이 메서드를 고치지 않아도 되도록.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkbenchDbContext).Assembly);
    }
}
