using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    internal const string SingleOwnerConstraintName = "CK_Comments_SingleOwner";

    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable(
            "Comments",
            t => t.HasCheckConstraint(SingleOwnerConstraintName, OwnershipSql.ExactlyOne("IssueId", "PageId")));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContentMarkdown).IsRequired();

        builder.HasIndex(c => new { c.IssueId, c.CreatedAt });
        builder.HasIndex(c => new { c.PageId, c.CreatedAt });

        builder.HasOne(c => c.Issue)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Page)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
