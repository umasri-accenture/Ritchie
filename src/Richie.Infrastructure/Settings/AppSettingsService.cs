using Richie.Application.Authentication;
using Richie.Application.Settings;
using Richie.Domain.Notifications;
using Richie.Domain.Settings;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Settings;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly IAppDbContextFactory _factory;
    private readonly IUserSession _session;

    public AppSettingsService(IAppDbContextFactory factory, IUserSession session)
    {
        _factory = factory;
        _session = session;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public AppSettingsData Get()
    {
        AppSettings row = Load();
        return new AppSettingsData(
            row.Theme, row.SessionLockMinutes, row.IncludeJewelleryInPortfolio, row.BackupFrequency,
            ParseTypes(row.DisabledNotificationTypes), row.LastBackupUtc);
    }

    public void Save(AppSettingsData settings)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        AppSettings? row = db.AppSettings.FirstOrDefault(s => s.UserId == userId);
        bool isNew = row is null;
        row ??= new AppSettings { UserId = userId };

        row.Theme = settings.Theme;
        row.SessionLockMinutes = Math.Clamp(settings.SessionLockMinutes, 1, 120);
        row.IncludeJewelleryInPortfolio = settings.IncludeJewelleryInPortfolio;
        row.BackupFrequency = settings.BackupFrequency;
        row.DisabledNotificationTypes = string.Join(",", settings.DisabledNotificationTypes.Select(t => t.ToString()));
        row.LastBackupUtc = settings.LastBackupUtc;

        if (isNew)
            db.AppSettings.Add(row);
        db.SaveChanges();
    }

    public bool IsNotificationEnabled(NotificationType type) =>
        !ParseTypes(Load().DisabledNotificationTypes).Contains(type);

    public void SetLastBackup(DateTime utc)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        AppSettings? row = db.AppSettings.FirstOrDefault(s => s.UserId == userId);
        bool isNew = row is null;
        row ??= new AppSettings { UserId = userId };
        row.LastBackupUtc = utc;
        if (isNew)
            db.AppSettings.Add(row);
        db.SaveChanges();
    }

    private AppSettings Load()
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        return db.AppSettings.FirstOrDefault(s => s.UserId == userId) ?? new AppSettings { UserId = userId };
    }

    public string GetStartupTheme()
    {
        try
        {
            using RichieDbContext db = _factory.Create();
            AppSettings? row = db.AppSettings.FirstOrDefault();
            return row?.Theme ?? "System";
        }
        catch
        {
            return "System";
        }
    }

    private static IReadOnlyCollection<NotificationType> ParseTypes(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse(s, out NotificationType t) ? (NotificationType?)t : null)
            .Where(t => t is not null)
            .Select(t => t!.Value)
            .ToHashSet();
}
