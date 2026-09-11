using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Interfaces;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Identity;
using Workbench.Infrastructure.Repositories;

namespace Workbench.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Workbench";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"접속 문자열 '{ConnectionStringName}' 이(가) 없습니다. " +
                $"User Secrets 또는 환경변수에 ConnectionStrings:{ConnectionStringName} 을(를) 설정하세요.");
        }

        var databaseSection = configuration.GetSection(DatabaseOptions.SectionName);
        services.Configure<DatabaseOptions>(databaseSection);
        var database = databaseSection.Get<DatabaseOptions>() ?? new DatabaseOptions();

        if (database.Provider == DatabaseProvider.Sqlite)
        {
            // SQLite 는 동시 쓰기를 직렬화하고 파일 하나에 담기며, 이 저장소의 마이그레이션은
            // SQL Server 전용이라 여기서는 EnsureCreated 로 스키마를 세운다 — 즉 운영에서
            // 쓸 수 있는 구성이 아니다. 탈출구를 두지 않는 이유가 그것이다.
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.Provider)} 가 "
                    + $"{nameof(DatabaseProvider.Sqlite)} 인데 호스트 환경이 Development 가 아닙니다. "
                    + "SQLite 는 SQL Server 없이 화면을 띄워 보기 위한 로컬 데모 전용이며 "
                    + $"마이그레이션도 적용되지 않습니다. {nameof(DatabaseProvider.SqlServer)} 를 쓰세요.");
            }

            services.AddDbContextFactory<WorkbenchDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            // Azure SQL 은 일시적 연결 끊김이 정상 동작 범위에 있다 — 재시도를 기본으로 켠다.
            services.AddDbContextFactory<WorkbenchDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        }

        // Blazor Server 의 스코프는 회로(circuit) 수명과 같아서 DbContext 가 몇 시간씩 살아남는다.
        // 팩토리를 두고 스코프마다 새 인스턴스를 만들어, 나중에 짧은 수명이 필요한 지점에서
        // 등록 방식을 바꾸지 않고 팩토리로 바로 갈아탈 수 있게 한다.
        services.AddScoped<WorkbenchDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WorkbenchDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserProvisioner, UserProvisioner>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<IPageRepository, PageRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();

        return services;
    }
}
