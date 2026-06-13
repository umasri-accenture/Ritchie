using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Richie.Domain.Assets;

namespace Richie.Infrastructure.Persistence.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.UserId);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Identifier).HasMaxLength(64);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.Exchange).HasMaxLength(64);
        builder.Property(a => a.PlatformName).HasMaxLength(120);
        builder.Property(a => a.PropertyAddress).HasMaxLength(400);
        builder.Property(a => a.Purity).HasMaxLength(40);
        builder.Property(a => a.AppraiserName).HasMaxLength(120);
        builder.Property(a => a.PolicyNumber).HasMaxLength(80);

        // decimal columns are stored as TEXT by the SQLite provider (invariant culture).
        // Allocation sums are computed in memory, so SQLite's decimal limitations don't bite.
    }
}
