using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    internal const string SingleOwnerConstraintName = "CK_Attachments_SingleOwner";

    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable(
            "Attachments",
            t => t.HasCheckConstraint(SingleOwnerConstraintName, OwnershipSql.ExactlyOne("IssueId", "PageId")));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.FileName);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.ContentType);

        builder.Property(a => a.BlobPath)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.BlobPath);

        // Blob 경로는 삭제/정합성 점검의 조회 키다.
        builder.HasIndex(a => a.BlobPath).IsUnique();

        builder.HasIndex(a => a.IssueId);
        builder.HasIndex(a => a.PageId);

        builder.HasOne(a => a.Issue)
            .WithMany(i => i.Attachments)
            .HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Page)
            .WithMany(p => p.Attachments)
            .HasForeignKey(a => a.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
