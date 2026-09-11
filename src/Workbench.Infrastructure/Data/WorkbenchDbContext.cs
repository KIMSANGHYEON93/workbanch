using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Workbench.Domain.Entities;
using Workbench.Domain.Interfaces;

namespace Workbench.Infrastructure.Data;

public class WorkbenchDbContext : DbContext, IUnitOfWork
{
    public WorkbenchDbContext(DbContextOptions<WorkbenchDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 파생 컨텍스트용. 테스트 하네스가 다른 프로바이더에 맞춘 규약을 얹으려면 자기 옵션 타입이
    /// 필요한데, 그 사정 때문에 운영 코드가 그 프로바이더를 알 필요는 없다.
    /// </summary>
    protected WorkbenchDbContext(DbContextOptions options)
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

    public void DiscardChanges() => ChangeTracker.Clear();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQLite 에는 datetimeoffset 에 대응하는 타입이 없어 EF 가 문자열로 저장하는데,
        // 그러면 ORDER BY 가 사전순이 되어 목록 정렬이 조용히 틀어진다. 이진 변환은
        // 정렬 가능한 표현을 주므로 이 프로바이더에서만 얹는다.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 설정 클래스를 파일 단위로 분리해 두고 어셈블리 스캔으로 등록한다 —
        // 엔티티가 늘어날 때 이 메서드를 고치지 않아도 되도록.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkbenchDbContext).Assembly);
    }
}
