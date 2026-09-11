using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.ProjectName);

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.ProjectKey);

        builder.Property(p => p.Description)
            .HasMaxLength(DomainConstants.Lengths.Description);

        builder.HasIndex(p => p.Key).IsUnique();

        builder.HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
