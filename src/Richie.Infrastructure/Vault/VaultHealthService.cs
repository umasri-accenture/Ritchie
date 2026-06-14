using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
using Richie.Application.Authentication;
using Richie.Application.Vault;
using Richie.Domain.Vault;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Vault;

/// <summary>
/// Password-health analysis (PRD §8.6). Decrypts each credential (vault must be unlocked) to detect:
/// <list type="bullet">
/// <item><b>Weak</b> — <see cref="PasswordStrength.IsWeak"/> (strength score ≤ 1).</item>
/// <item><b>Reused</b> — the same password value appears on more than one credential.</item>
/// <item><b>Aged</b> — not changed in over 90 days (and flagged more severely past 180).</item>
/// </list>
/// Scoring (transparent, defined here — not a locked spec; weights are tunable): each credential
/// starts at 100 and loses points per issue (weak −40, reused −30, aged&gt;180d −20 else &gt;90d −10),
/// floored at 0. The overall score is the average across all credentials (empty vault = 100).
/// Rating scale: ≥80 Good · 50–79 Needs attention · &lt;50 Critical.
/// </summary>
public sealed class VaultHealthService : IVaultHealthService
{
    private const int WeakPenalty = 40;
    private const int ReusedPenalty = 30;
    private const int VeryAgedPenalty = 20;   // > 180 days
    private const int AgedPenalty = 10;        // 90–180 days
    private const int AgeWarnDays = 90;
    private const int AgeCriticalDays = 180;

    private readonly IAppDbContextFactory _factory;
    private readonly IUserSession _session;
    private readonly IVaultGate _gate;
    private readonly IClock _clock;

    public VaultHealthService(IAppDbContextFactory factory, IUserSession session, IVaultGate gate, IClock clock)
    {
        _factory = factory;
        _session = session;
        _gate = gate;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public VaultHealthReport GetReport()
    {
        Guid userId = UserId;
        DateTime now = _clock.UtcNow;

        using RichieDbContext db = _factory.Create();
        List<VaultEntry> entries = db.VaultEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToList();

        if (entries.Count == 0)
            return new VaultHealthReport(100, "Good", 0, 0, 0, 0, []);

        // Decrypt once; count how many credentials share each password value.
        var decrypted = entries
            .Select(e => new { Entry = e, Password = _gate.Decrypt(e.PasswordCipher) })
            .ToList();
        Dictionary<string, int> passwordCounts = decrypted
            .GroupBy(x => x.Password, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        int weakCount = 0, reusedCount = 0, agedCount = 0;
        var items = new List<VaultHealthEntry>();
        int scoreSum = 0;

        foreach (var x in decrypted)
        {
            int score = 100;
            var issues = new List<string>();

            if (PasswordStrength.Evaluate(x.Password).IsWeak)
            {
                score -= WeakPenalty;
                weakCount++;
                issues.Add("Weak password");
            }

            if (passwordCounts[x.Password] > 1)
            {
                score -= ReusedPenalty;
                reusedCount++;
                issues.Add("Reused on another account");
            }

            int ageDays = (int)(now - x.Entry.PasswordUpdatedUtc).TotalDays;
            if (ageDays > AgeCriticalDays)
            {
                score -= VeryAgedPenalty;
                agedCount++;
                issues.Add($"Not changed in over {AgeCriticalDays} days");
            }
            else if (ageDays > AgeWarnDays)
            {
                score -= AgedPenalty;
                agedCount++;
                issues.Add($"Not changed in over {AgeWarnDays} days");
            }

            if (score < 0) score = 0;
            scoreSum += score;

            if (issues.Count > 0)
                items.Add(new VaultHealthEntry(x.Entry.Id, x.Entry.AccountName, score, issues));
        }

        int overall = (int)Math.Round((double)scoreSum / decrypted.Count);
        items = items.OrderBy(i => i.Score).ThenBy(i => i.AccountName).ToList();

        return new VaultHealthReport(overall, Rate(overall), decrypted.Count,
            weakCount, reusedCount, agedCount, items);
    }

    private static string Rate(int score) => score >= 80 ? "Good" : score >= 50 ? "Needs attention" : "Critical";
}
