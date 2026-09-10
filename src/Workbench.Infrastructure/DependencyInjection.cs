using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Domain.Interfaces;
using Workbench.Infrastructure.Data;
using Workbench.Infrastructure.Repositories;

namespace Workbench.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Workbench";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"접속 문자열 '{ConnectionStringName}' 이(가) 없습니다. " +
                $"User Secrets 또는 환경변수에 ConnectionStrings:{ConnectionStringName} 을(를) 설정하세요.");
        }

        // Azure SQL 은 일시적 연결 끊김이 정상 동작 범위에 있다 — 재시도를 기본으로 켠다.
        services.AddDbContextFactory<WorkbenchDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Blazor Server 의 스코프는 회로(circuit) 수명과 같아서 DbContext 가 몇 시간씩 살아남는다.
        // 팩토리를 두고 스코프마다 새 인스턴스를 만들어, 나중에 짧은 수명이 필요한 지점에서
        // 등록 방식을 바꾸지 않고 팩토리로 바로 갈아탈 수 있게 한다.
        services.AddScoped<WorkbenchDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WorkbenchDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
