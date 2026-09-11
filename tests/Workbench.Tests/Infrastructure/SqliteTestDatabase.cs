using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Workbench.Application;
using Workbench.Application.Interfaces;
using Workbench.Application.Services;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// 실제 EF 프로바이더로 도는 테스트용 DB + 그 위에 올린 실제 서비스 조합.
/// 서비스 조립을 여기서 소유하는 이유는, 생성자 시그니처가 바뀔 때 종단 테스트 다섯 개를
/// 각각 고치지 않기 위해서다.
/// ⚠ SQLite 는 SQL Server 가 아니다 — 스키마 세부(인덱스 키 크기, datetimeoffset 컬럼 타입 등)는
/// 여기서 검증되지 않으며 모델 형상 계약이 담당한다.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Context = new SqliteWorkbenchDbContext(
            new DbContextOptionsBuilder<SqliteWorkbenchDbContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated();

        Context.Users.Add(new AppUser
        {
            Id = FakeCurrentUser.DefaultUserId,
            DisplayName = "홍길동",
            Email = "gildong@example.com",
        });
        Context.SaveChanges();

        CurrentUser = new FakeCurrentUser();

        var projectRepository = new ProjectRepository(Context);
        var memberRepository = new ProjectMemberRepository(Context);
        var issueRepository = new IssueRepository(Context);
        var pageRepository = new PageRepository(Context);
        var userRepository = new AppUserRepository(Context);

        Access = new ProjectAccessService(
            memberRepository, projectRepository, userRepository, Context, CurrentUser);
        Projects = new ProjectService(
            projectRepository, memberRepository, Access, Context, CurrentUser);
        Pages = new PageService(pageRepository, projectRepository, Access, Context, CurrentUser);
        Issues = new IssueService(
            issueRepository,
            projectRepository,
            userRepository,
            Access,
            Context,
            CurrentUser,
            NullLogger<IssueService>.Instance);
        Comments = new CommentService(
            new CommentRepository(Context), issueRepository, pageRepository, Context, CurrentUser);
    }

    public WorkbenchDbContext Context { get; }

    public FakeCurrentUser CurrentUser { get; }

    public ProjectAccessService Access { get; }

    public ProjectService Projects { get; }

    public IssueService Issues { get; }

    public PageService Pages { get; }

    public CommentService Comments { get; }

    /// <summary>첨부는 저장소 구현과 상한이 시험마다 달라서 여기서 조립하지 않고 만들어 준다.</summary>
    public AttachmentService CreateAttachmentService(
        IFileStorage storage,
        AttachmentOptions? options = null) =>
        new(
            new AttachmentRepository(Context),
            new IssueRepository(Context),
            new PageRepository(Context),
            storage,
            Context,
            CurrentUser,
            Options.Create(options ?? new AttachmentOptions()),
            NullLogger<AttachmentService>.Instance);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// SQLite 는 <c>ORDER BY</c> 에서 <see cref="DateTimeOffset"/> 을 지원하지 않는다.
    /// 목록 화면은 전부 시각으로 정렬하므로, 그 제약을 테스트 쪽에서 흡수한다 —
    /// 운영 프로바이더(SQL Server)는 datetimeoffset 을 그대로 정렬하므로 손대지 않는다.
    /// </summary>
    private sealed class SqliteWorkbenchDbContext : WorkbenchDbContext
    {
        public SqliteWorkbenchDbContext(DbContextOptions<SqliteWorkbenchDbContext> options)
            : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}
