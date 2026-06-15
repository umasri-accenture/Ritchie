using System.IO;
using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
using Richie.Application.Authentication;
using Richie.Application.Profile;
using Richie.Application.Settings;
using Richie.Application.Vault;
using Richie.Domain.Auditing;
using Richie.Domain.Authentication;
using Richie.Infrastructure.Auditing;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Profile;

public sealed class ProfileService : IProfileService
{
    private readonly IAppDbContextFactory _factory;
    private readonly IUserSession _session;
    private readonly IVaultGate _gate;
    private readonly IVaultHealthService _vaultHealth;
    private readonly IAppSettingsService _settings;
    private readonly IClock _clock;

    public ProfileService(
        IAppDbContextFactory factory, IUserSession session, IVaultGate gate,
        IVaultHealthService vaultHealth, IAppSettingsService settings, IClock clock)
    {
        _factory = factory;
        _session = session;
        _gate = gate;
        _vaultHealth = vaultHealth;
        _settings = settings;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public ProfileData Get()
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        User user = db.Users.AsNoTracking().First(u => u.Id == userId);

        (int score, string note) = SecurityScore();
        return new ProfileData(user.FullName, user.Username, user.Age, user.City, score, note, StorageBytes());
    }

    public bool Update(ProfileUpdate update)
    {
        if (string.IsNullOrWhiteSpace(update.FullName) || update.Age is < 1 or > 120)
            return false;

        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        User? user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return false;

        user.FullName = update.FullName.Trim();
        user.Age = update.Age;
        user.City = update.City?.Trim() ?? string.Empty;
        AuditWriter.Add(db, userId, _clock.UtcNow, "Profile", AuditAction.Update, nameof(User), userId, "Updated profile.");
        db.SaveChanges();
        return true;
    }

    // Transparent security score (0–100): vault set up (30) + recovery enabled (20) +
    // short auto-lock (20) + password health (30, only when the vault is unlocked).
    private (int Score, string Note) SecurityScore()
    {
        int score = 0;
        var parts = new List<string>();

        if (_gate.IsConfigured()) { score += 30; parts.Add("vault configured (+30)"); }
        else parts.Add("vault not set up (0/30)");

        if (_gate.IsConfigured() && _gate.IsRecoveryEnabled()) { score += 20; parts.Add("recovery enabled (+20)"); }
        else parts.Add("no recovery (0/20)");

        if (_settings.Get().SessionLockMinutes <= 15) { score += 20; parts.Add("auto-lock ≤15 min (+20)"); }
        else parts.Add("long auto-lock (0/20)");

        if (_gate.IsUnlocked)
        {
            int health = _vaultHealth.GetReport().Score;
            int contribution = (int)Math.Round(health * 0.30);
            score += contribution;
            parts.Add($"password health {health}/100 (+{contribution})");
        }
        else
        {
            parts.Add("unlock the vault for the password-health portion (0/30)");
        }

        return (Math.Clamp(score, 0, 100), string.Join(" · ", parts));
    }

    public IReadOnlyList<Achievement> GetAchievements()
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();

        int assets = db.Assets.AsNoTracking().Count(a => a.UserId == userId);
        int distinctTypes = db.Assets.AsNoTracking().Where(a => a.UserId == userId)
            .Select(a => a.Type).Distinct().Count();
        int expenses = db.Expenses.AsNoTracking().Count(e => e.UserId == userId);
        int vault = db.VaultEntries.AsNoTracking().Count(v => v.UserId == userId);
        int policies = db.InsurancePolicies.AsNoTracking().Count(p => p.UserId == userId);
        int goals = db.Goals.AsNoTracking().Count(g => g.UserId == userId);

        return
        [
            new Achievement("First Asset", "Add your first asset", "🏦", assets >= 1),
            new Achievement("Diversifier", "Hold 3 or more asset types", "🧩", distinctTypes >= 3),
            new Achievement("Expense Tracker", "Log 25 or more expenses", "🧾", expenses >= 25),
            new Achievement("Vault Keeper", "Store a password in the vault", "🔐", _gate.IsConfigured() && vault >= 1),
            new Achievement("Protected", "Add an insurance policy", "🛡️", policies >= 1),
            new Achievement("Goal Setter", "Create a financial goal", "🎯", goals >= 1),
        ];
    }

    private static long StorageBytes()
    {
        if (!Directory.Exists(AppPaths.DataDirectory))
            return 0;
        long total = 0;
        foreach (string file in Directory.EnumerateFiles(AppPaths.DataDirectory, "*", SearchOption.AllDirectories))
            total += new FileInfo(file).Length;
        return total;
    }
}
