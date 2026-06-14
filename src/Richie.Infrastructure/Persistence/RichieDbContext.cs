using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Richie.Domain.Assets;
using Richie.Domain.Auditing;
using Richie.Domain.Authentication;
using Richie.Domain.Expenses;
using Richie.Domain.Notifications;

namespace Richie.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context over the SQLCipher-encrypted database.
/// Entity configurations live in <c>Persistence/Configurations</c> and are applied by convention.
/// </summary>
public class RichieDbContext : DbContext
{
    public RichieDbContext(DbContextOptions<RichieDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDocument> AssetDocuments => Set<AssetDocument>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<AssetGoalLink> AssetGoalLinks => Set<AssetGoalLink>();
    public DbSet<SipSchedule> SipSchedules => Set<SipSchedule>();
    public DbSet<SipContribution> SipContributions => Set<SipContribution>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
