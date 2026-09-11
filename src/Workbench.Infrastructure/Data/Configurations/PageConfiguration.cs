using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.PageTitle);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.Slug);

        builder.Property(p => p.ContentMarkdown)
            .IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();

        builder.HasIndex(p => p.ParentPageId);

        // 자기참조 관계는 SQL Server 에서 CASCADE 를 걸 수 없다(순환 경로).
        // 부모를 지울 때 자식을 어떻게 할지는 애플리케이션이 명시적으로 정한다.
        builder.HasOne(p => p.ParentPage)
            .WithMany(p => p.Children)
            .HasForeignKey(p => p.ParentPageId)
            .OnDelete(DeleteBehavior.Restrict);

        // 프로젝트를 지워도 지식 문서는 남긴다 — 연결만 끊는다.
        builder.HasOne(p => p.Project)
            .WithMany(p => p.Pages)
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
