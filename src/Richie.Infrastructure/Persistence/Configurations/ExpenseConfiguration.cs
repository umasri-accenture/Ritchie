using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Richie.Domain.Expenses;

namespace Richie.Infrastructure.Persistence.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.UserId, e.Date });
        builder.Property(e => e.SpentBy).HasMaxLength(120);
        builder.Property(e => e.SpentFor).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(2000);
    }
}
