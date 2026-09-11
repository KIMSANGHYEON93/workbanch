using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Workbench.Domain;
using Workbench.Domain.Entities;
using Workbench.Domain.Enums;
using Workbench.Infrastructure.Data;

namespace Workbench.Web.Services;

/// <summary>
/// SQLite 데모 모드에서만 도는 초기화. 마이그레이션은 SQL Server 전용이라 스키마를
/// <c>EnsureCreated</c> 로 세우고, 빈 화면 대신 훑어볼 것이 있도록 예시 데이터를 넣는다.
/// <b>SQL Server 경로에는 어떤 영향도 주지 않는다</b> — 프로바이더가 SQLite 가 아니면 즉시 반환한다.
/// </summary>
public static class DemoDataInitializer
{
    public static async Task InitializeDemoDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var database = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (database.Provider != DatabaseProvider.Sqlite)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorkbenchDbContext>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DemoDataInitializer));

        EnsureDatabaseDirectoryExists(
            context.Database.GetConnectionString(), app.Environment.ContentRootPath);

        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (!database.SeedDemoData)
        {
            return;
        }

        // 두 번 켜도 같은 결과여야 한다 — 프로젝트가 하나라도 있으면 손대지 않는다.
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var developer = app.Services.GetRequiredService<IOptions<WorkbenchAuthenticationOptions>>()
            .Value.DevelopmentUser;

        var me = await context.Users.FirstOrDefaultAsync(
            u => u.Id == developer.Id, cancellationToken);

        if (me is null)
        {
            me = new AppUser
            {
                Id = developer.Id,
                DisplayName = developer.DisplayName,
                Email = developer.Email,
            };
            context.Users.Add(me);
        }

        var teammate = new AppUser
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DisplayName = "박서연",
            Email = "seoyeon@example.com",
        };
        context.Users.Add(teammate);

        Seed(context, me, teammate);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SQLite 데모 데이터를 넣었습니다. 운영 구성(SQL Server)에서는 이 경로가 실행되지 않습니다.");
    }

    /// <summary>
    /// SQLite 는 디렉터리를 만들지 않는다 — 파일 경로의 상위 폴더가 없으면 "unable to open
    /// database file" 로 기동이 막힌다. 새로 clone 한 기계에서 항상 걸리는 지점이라 여기서 만든다.
    /// </summary>
    private static void EnsureDatabaseDirectoryExists(string? connectionString, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource.StartsWith(':'))
        {
            return;
        }

        // 상대 경로는 프로세스 작업 디렉터리가 아니라 콘텐츠 루트 기준이어야 한다 —
        // 그래야 어느 디렉터리에서 dotnet run 을 하든 같은 파일을 연다.
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource, contentRoot));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void Seed(WorkbenchDbContext context, AppUser me, AppUser teammate)
    {
        var now = DateTimeOffset.UtcNow;

        var workbench = NewProject(
            "Workbench 도입",
            "WB",
            "Microsoft Planner 를 대체할 사내 이슈 추적 + 지식 저장소.",
            me,
            now.AddDays(-24));

        var infra = NewProject(
            "인프라 운영",
            "INFRA",
            "사내 서버·네트워크 운영 요청 창구.",
            me,
            now.AddDays(-11));

        context.Projects.AddRange(workbench, infra);

        context.ProjectMembers.AddRange(
            NewMember(workbench, me, ProjectRole.Admin),
            NewMember(workbench, teammate, ProjectRole.Member),
            NewMember(infra, me, ProjectRole.Admin));

        var issues = new List<Issue>
        {
            NewIssue(workbench, me, "칸반 보드에서 상태를 드래그로 바꿀 수 있게 한다",
                IssueType.Story, IssueStatus.Done, IssuePriority.High, me, now.AddDays(-21),
                "보드에서 카드를 끌어다 놓으면 상태가 바뀌어야 한다.\n\n- [x] 드롭 대상 표시\n- [x] 낙관적 갱신\n- [x] 실패 시 되돌리기"),
            NewIssue(workbench, me, "첨부 파일 다운로드가 브라우저에서 바로 열린다",
                IssueType.Bug, IssueStatus.Done, IssuePriority.Highest, teammate, now.AddDays(-19),
                "`Content-Disposition` 이 `inline` 이라 HTML 첨부가 **실행**된다.\n\n내려보낼 때 항상 `attachment` 로 강제할 것."),
            NewIssue(workbench, me, "페이지 계층에서 순환 참조를 막는다",
                IssueType.Bug, IssueStatus.InReview, IssuePriority.High, me, now.AddDays(-9),
                "자기 자손을 부모로 지정하면 목록 렌더가 무한 루프에 빠진다."),
            NewIssue(workbench, me, "이슈 목록에 담당자·우선순위 필터를 붙인다",
                IssueType.Task, IssueStatus.InProgress, IssuePriority.Medium, teammate, now.AddDays(-6),
                "쿼리스트링으로 상태가 남아야 새로고침·공유가 된다."),
            NewIssue(workbench, me, "마크다운에 붙여넣은 HTML 이 그대로 렌더된다",
                IssueType.Bug, IssueStatus.Todo, IssuePriority.Highest, null, now.AddDays(-4),
                "`<img src=x onerror=...>` 가 실행된다. 렌더러에서 원시 HTML 을 꺼야 한다."),
            NewIssue(workbench, me, "전역 검색에 페이지 본문을 포함한다",
                IssueType.Story, IssueStatus.Backlog, IssuePriority.Medium, null, now.AddDays(-3),
                "지금은 제목만 찾는다. 본문까지 훑되 결과 개수 상한을 둔다."),
            NewIssue(workbench, me, "Entra ID 앱 등록 후 실제 로그인 경로 확인",
                IssueType.Task, IssueStatus.Backlog, IssuePriority.High, me, now.AddDays(-2),
                "개발용 우회를 끄고 실제 테넌트로 한 번 끝까지 통과시켜 볼 것."),
            NewIssue(infra, me, "야간 배치 로그 보존 기간을 90일로 늘린다",
                IssueType.Task, IssueStatus.InProgress, IssuePriority.Low, me, now.AddDays(-8),
                "감사 요청 때 30일치로는 부족했다."),
            NewIssue(infra, me, "사내 위키 서버 디스크 사용률 경보",
                IssueType.Bug, IssueStatus.Todo, IssuePriority.High, teammate, now.AddDays(-1),
                "`/var` 사용률 91%. 오래된 업로드 임시파일이 정리되지 않는다."),
            NewIssue(infra, me, "VPN 동시 접속 한도 상향 검토",
                IssueType.Story, IssueStatus.Backlog, IssuePriority.Low, null, now.AddHours(-20),
                "재택 인원이 늘면서 오전 9시대에 거절이 발생한다."),
        };

        context.Issues.AddRange(issues);

        var guide = NewPage(
            workbench,
            me,
            "Workbench 사용 안내",
            now.AddDays(-20),
            """
            # Workbench 사용 안내

            Planner 에서 넘어오신 분들을 위한 짧은 안내입니다.

            ## 이슈와 페이지

            | 개념 | 쓰는 곳 |
            |---|---|
            | **이슈** | 끝이 있는 일. 상태가 바뀌고 담당자가 있다. |
            | **페이지** | 끝이 없는 지식. 계층으로 쌓이고 계속 고쳐 쓴다. |

            ## 이슈 키

            프로젝트 키 + 연속 번호입니다 — `WB-1`, `INFRA-3`. 이슈를 지워도
            번호는 재사용되지 않습니다.

            > 삭제 이력 때문에 `COUNT(*)` 로 다음 번호를 뽑으면 충돌합니다.
            > 발급 카운터를 프로젝트에 따로 두는 이유입니다.
            """);

        var onboarding = NewPage(
            workbench,
            me,
            "신규 입사자 온보딩",
            now.AddDays(-14),
            """
            # 신규 입사자 온보딩

            1. 계정 발급 요청 (INFRA 프로젝트에 이슈 등록)
            2. 사내 VPN 설치
            3. 저장소 접근 권한 요청
            4. 개발 환경 구성 — 아래 하위 페이지 참고
            """);

        var localSetup = NewPage(
            workbench,
            me,
            "로컬 개발 환경 구성",
            now.AddDays(-13),
            """
            # 로컬 개발 환경 구성

            ```bash
            git clone https://github.com/KIMSANGHYEON93/workbanch
            cd workbanch
            dotnet tool restore
            dotnet run --project src/Workbench.Web
            ```

            기본값은 SQLite 데모 모드라 **SQL Server 없이도 바로 뜹니다**.
            실제 DB 로 붙이려면 `appsettings.Development.json` 의
            `Database:Provider` 를 `SqlServer` 로 바꾸고 마이그레이션을 적용하세요.
            """);
        localSetup.ParentPage = onboarding;

        var runbook = NewPage(
            infra,
            me,
            "장애 대응 런북",
            now.AddDays(-7),
            """
            # 장애 대응 런북

            ## 1. 영향 범위 먼저

            누가 못 쓰고 있는지부터 적는다. 원인은 그 다음이다.

            ## 2. 연락 순서

            - 1차: 당번 (사내 메신저 `#infra-oncall`)
            - 2차: 팀 리드
            - 30분 내 복구 불가 시 공지 채널에 상황 공유

            ## 3. 사후

            재발 방지 항목을 **이슈로** 남긴다. 문서에만 적으면 아무도 안 한다.
            """);

        context.Pages.AddRange(guide, onboarding, localSetup, runbook);

        context.Comments.AddRange(
            new Comment
            {
                Issue = issues[1],
                Author = teammate,
                ContentMarkdown = "재현했습니다. `.html` 첨부를 올리면 새 탭에서 바로 실행됩니다.",
                CreatedAt = now.AddDays(-18),
            },
            new Comment
            {
                Issue = issues[1],
                Author = me,
                ContentMarkdown = "`Results.File(..., fileDownloadName)` 로 바꿔서 항상 `attachment` 가 되도록 했습니다.",
                CreatedAt = now.AddDays(-18).AddHours(3),
            },
            new Comment
            {
                Issue = issues[4],
                Author = me,
                ContentMarkdown = "Markdig 파이프라인에서 `DisableHtml()` 만으로는 부족합니다 — 링크 스킴도 막아야 합니다.",
                CreatedAt = now.AddDays(-3),
            },
            new Comment
            {
                Page = runbook,
                Author = teammate,
                ContentMarkdown = "2차 연락처에 휴가 중 대체자를 적어 두면 좋겠습니다.",
                CreatedAt = now.AddDays(-5),
            });
    }

    private static Project NewProject(
        string name, string key, string description, AppUser owner, DateTimeOffset createdAt) =>
        new()
        {
            Name = name,
            Key = ProjectKey.Normalize(key),
            Description = description,
            CreatedBy = owner,
            CreatedAt = createdAt,
        };

    private static ProjectMember NewMember(Project project, AppUser user, ProjectRole role) =>
        new() { Project = project, User = user, Role = role, CreatedAt = project.CreatedAt };

    private static Issue NewIssue(
        Project project,
        AppUser reporter,
        string title,
        IssueType type,
        IssueStatus status,
        IssuePriority priority,
        AppUser? assignee,
        DateTimeOffset createdAt,
        string description)
    {
        var number = ++project.LastIssueNumber;

        return new Issue
        {
            Project = project,
            Number = number,
            Key = Issue.FormatKey(project.Key, number),
            Title = title,
            DescriptionMarkdown = description,
            Type = type,
            Status = status,
            Priority = priority,
            Assignee = assignee,
            Reporter = reporter,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    private static Page NewPage(
        Project project, AppUser author, string title, DateTimeOffset createdAt, string content) =>
        new()
        {
            Project = project,
            Title = title,
            Slug = PageSlug.FromTitle(title),
            ContentMarkdown = content,
            CreatedBy = author,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsPublished = true,
        };
}
