using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Workbench.Domain.Entities;
using Workbench.Infrastructure.Data;
using Workbench.Tests.Fakes;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// 실제 EF 프로바이더로 도는 테스트용 DB. 쿼리가 정말 번역·실행되는지를 보는 것이 목적이다.
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
    }

    public WorkbenchDbContext Context { get; }

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
