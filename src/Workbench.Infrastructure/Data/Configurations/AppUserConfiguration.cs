using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workbench.Domain;
using Workbench.Domain.Entities;

namespace Workbench.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // Entra Object Id 를 그대로 PK 로 쓴다 — DB 가 값을 만들지 않게 막는다.
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.DisplayName);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(DomainConstants.Lengths.Email);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
