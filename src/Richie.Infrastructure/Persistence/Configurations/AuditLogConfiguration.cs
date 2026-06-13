using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Richie.Domain.Auditing;

namespace Richie.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.TimestampUtc);
        builder.Property(a => a.Module).IsRequired().HasMaxLength(64);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Description).HasMaxLength(500);
    }
}
