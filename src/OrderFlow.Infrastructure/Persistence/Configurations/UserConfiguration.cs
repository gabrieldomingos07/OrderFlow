using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Users;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int PasswordHashMaxLength = 512;
    private const int RoleMaxLength = 20;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email).HasMaxLength(User.EmailMaxLength);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.FullName).HasMaxLength(User.FullNameMaxLength);
        builder.Property(user => user.PasswordHash).HasMaxLength(PasswordHashMaxLength);
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(RoleMaxLength);
    }
}
