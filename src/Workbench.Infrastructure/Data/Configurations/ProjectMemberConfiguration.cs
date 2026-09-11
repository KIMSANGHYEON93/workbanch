using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");

        builder.HasKey(m => m.Id);

        // 같은 사람이 한 프로젝트에 두 역할로 들어가면 권한 판정이 비결정적이 된다.
        builder.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();

        builder.HasOne(m => m.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
