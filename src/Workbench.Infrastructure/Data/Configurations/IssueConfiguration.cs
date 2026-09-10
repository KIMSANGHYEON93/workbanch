using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Key)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.IssueKey);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.IssueTitle);

        builder.Property(i => i.Environment)
            .HasMaxLength(DomainConstants.Lengths.EnvironmentName);

        builder.Property(i => i.Version)
            .HasMaxLength(DomainConstants.Lengths.VersionLabel);

        builder.HasIndex(i => i.Key).IsUnique();

        // 키의 두 구성요소 자체도 유일해야 한다 — Key 문자열만 막으면 번호를 중복 발급하고도
        // 다른 프로젝트 접두어를 붙여 통과시킬 수 있다.
        builder.HasIndex(i => new { i.ProjectId, i.Number }).IsUnique();

        // 보드/리스트의 기본 조회 축.
        builder.HasIndex(i => new { i.ProjectId, i.Status });
        builder.HasIndex(i => i.AssigneeId);

        builder.HasOne(i => i.Project)
            .WithMany(p => p.Issues)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // 사용자는 삭제하지 않고 IsActive 로 비활성화한다 — 담당자/보고자 이력을 보존.
        builder.HasOne(i => i.Assignee)
            .WithMany()
            .HasForeignKey(i => i.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Reporter)
            .WithMany()
            .HasForeignKey(i => i.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
