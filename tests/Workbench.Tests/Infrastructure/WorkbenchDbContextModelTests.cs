using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Workbench.Infrastructure.Data;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// 모델 형상 계약. EF 는 DB 접속 없이 모델을 조립하므로 여기서 검사하는 것은
/// 전부 실 DB 없이 성립한다 — 마이그레이션을 만들기 전에 스키마 실수를 잡는 것이 목적이다.
/// </summary>
public class WorkbenchDbContextModelTests
{
    /// <summary>SQL Server 비클러스터드 인덱스 키 상한(2016+).</summary>
    private const int SqlServerMaxIndexKeyBytes = 1700;

    /// <summary>
    /// 길이 제한 없이 두는 것이 의도인 컬럼. 마크다운 본문은 상한을 두면 사용자가 글을 잃는다.
    /// 이 목록에 없는 문자열 컬럼이 nvarchar(max) 로 새는 것을 아래 계약이 막는다.
    /// </summary>
    private static readonly HashSet<string> IntentionallyUnbounded =
    [
        "Issue.DescriptionMarkdown",
        "Page.ContentMarkdown",
        "Comment.ContentMarkdown",
    ];

    private static WorkbenchDbContext CreateContext() =>
        new WorkbenchDbContextFactory().CreateDbContext([]);

    /// <summary>
    /// CHECK 제약 같은 일부 설정은 런타임용 읽기 최적화 모델(<c>DbContext.Model</c>)에 실리지 않는다.
    /// 마이그레이션이 보는 것과 같은 모델을 봐야 스키마 계약을 검사할 수 있다.
    /// </summary>
    private static IModel DesignTimeModel(WorkbenchDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    [Theory]
    [InlineData("AppUser", "Users")]
    [InlineData("Project", "Projects")]
    [InlineData("ProjectMember", "ProjectMembers")]
    [InlineData("Issue", "Issues")]
    [InlineData("Page", "Pages")]
    [InlineData("Comment", "Comments")]
    [InlineData("Attachment", "Attachments")]
    public void Entities_MapToExpectedTables(string entityName, string tableName)
    {
        using var context = CreateContext();

        var entityType = DesignTimeModel(context).GetEntityTypes()
            .SingleOrDefault(e => e.ClrType.Name == entityName);

        Assert.NotNull(entityType);
        Assert.Equal(tableName, entityType.GetTableName());
    }

    [Theory]
    [InlineData("Comments", "CK_Comments_SingleOwner")]
    [InlineData("Attachments", "CK_Attachments_SingleOwner")]
    public void OwnedByExactlyOneParent_IsEnforcedByCheckConstraint(string tableName, string constraintName)
    {
        using var context = CreateContext();

        var entityType = DesignTimeModel(context).GetEntityTypes()
            .Single(e => e.GetTableName() == tableName);

        var constraint = entityType.GetCheckConstraints()
            .SingleOrDefault(c => c.Name == constraintName);

        Assert.NotNull(constraint);

        // 제약이 "정확히 하나"를 강제하는지까지 본다 — 이름만 맞고 식이 비면 공허하다.
        Assert.Contains("IssueId", constraint.Sql);
        Assert.Contains("PageId", constraint.Sql);
        Assert.Contains("= 1", constraint.Sql);
    }

    [Theory]
    [InlineData("Users", new[] { "Email" })]
    [InlineData("Projects", new[] { "Key" })]
    [InlineData("Issues", new[] { "Key" })]
    [InlineData("Issues", new[] { "ProjectId", "Number" })]
    [InlineData("Pages", new[] { "Slug" })]
    [InlineData("ProjectMembers", new[] { "ProjectId", "UserId" })]
    [InlineData("Attachments", new[] { "BlobPath" })]
    public void UniquenessRules_AreEnforcedByIndexes(string tableName, string[] columns)
    {
        using var context = CreateContext();

        var entityType = DesignTimeModel(context).GetEntityTypes()
            .Single(e => e.GetTableName() == tableName);

        var match = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(columns));

        Assert.True(match is not null, $"{tableName}({string.Join(", ", columns)}) 에 UNIQUE 인덱스가 없다.");
    }

    [Fact]
    public void UniqueIndexKeys_StayUnderSqlServerKeyLimit()
    {
        using var context = CreateContext();

        var uniqueIndexes = DesignTimeModel(context).GetEntityTypes()
            .SelectMany(e => e.GetIndexes())
            .Where(i => i.IsUnique)
            .ToList();

        // 탐지기 비공허성: 검사 대상이 0개면 아래 Assert 는 아무것도 지키지 못한다.
        Assert.NotEmpty(uniqueIndexes);

        var offenders = uniqueIndexes
            .Select(index => new
            {
                Name = $"{index.DeclaringEntityType.GetTableName()}"
                    + $"({string.Join(", ", index.Properties.Select(p => p.Name))})",
                Bytes = index.Properties.Sum(EstimateMaxKeyBytes),
            })
            .Where(x => x.Bytes > SqlServerMaxIndexKeyBytes)
            .Select(x => $"{x.Name} = {x.Bytes}B")
            .ToList();

        // 초과분은 생성 시점이 아니라 긴 값을 INSERT 하는 시점에 터진다 — 배포 뒤에야 드러난다.
        Assert.True(offenders.Count == 0, string.Join(" · ", offenders));
    }

    [Fact]
    public void StringColumns_DeclareALengthUnlessIntentionallyUnbounded()
    {
        using var context = CreateContext();

        var unbounded = DesignTimeModel(context).GetEntityTypes()
            .SelectMany(e => e.GetProperties()
                .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null)
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .Where(name => !IntentionallyUnbounded.Contains(name))
            .ToList();

        Assert.True(unbounded.Count == 0, string.Join(" · ", unbounded));
    }

    [Fact]
    public void DeletingAProject_KeepsItsKnowledgePages()
    {
        using var context = CreateContext();

        var entityType = DesignTimeModel(context).GetEntityTypes().Single(e => e.GetTableName() == "Pages");
        var toProject = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "Projects");

        Assert.Equal(DeleteBehavior.SetNull, toProject.DeleteBehavior);
    }

    [Fact]
    public void PageHierarchy_DoesNotCascade()
    {
        using var context = CreateContext();

        var entityType = DesignTimeModel(context).GetEntityTypes().Single(e => e.GetTableName() == "Pages");
        var selfReference = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "Pages");

        // SQL Server 는 자기참조 CASCADE 를 만들 수 없다. 여기가 Cascade 가 되면 마이그레이션이 깨진다.
        Assert.NotEqual(DeleteBehavior.Cascade, selfReference.DeleteBehavior);
    }

    private static int EstimateMaxKeyBytes(IProperty property)
    {
        if (property.ClrType == typeof(string))
        {
            var maxLength = property.GetMaxLength();

            // 길이 미지정 문자열은 nvarchar(max) 라 인덱스 키가 될 수 없다.
            return maxLength is null
                ? int.MaxValue / 2
                : maxLength.Value * ((property.IsUnicode() ?? true) ? 2 : 1);
        }

        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        if (clrType == typeof(Guid))
        {
            return 16;
        }

        if (clrType == typeof(DateTimeOffset))
        {
            return 10;
        }

        if (clrType == typeof(long) || clrType == typeof(DateTime))
        {
            return 8;
        }

        if (clrType == typeof(bool) || clrType == typeof(byte))
        {
            return 1;
        }

        // int · enum · DateOnly 등. 알 수 없는 타입은 넉넉하게 잡는다(과소평가가 위험한 방향).
        return clrType.IsEnum || clrType == typeof(int) ? 4 : 16;
    }
}
