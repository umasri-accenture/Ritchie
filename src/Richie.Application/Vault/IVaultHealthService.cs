namespace Richie.Application.Vault;

/// <summary>A credential that needs attention, with its per-credential health score (0–100) and
/// the human-readable issues found, worst-first.</summary>
public sealed record VaultHealthEntry(Guid Id, string AccountName, int Score, IReadOnlyList<string> Issues);

/// <summary>
/// Overall password-health report (PRD §8.6). <see cref="Score"/> is 0–100 with an explicit scale
/// (see <see cref="VaultHealthService"/> docs / the UI legend): ≥80 Good, 50–79 Needs attention,
/// &lt;50 Critical. <see cref="Items"/> lists only credentials with at least one issue.
/// </summary>
public sealed record VaultHealthReport(
    int Score,
    string Rating,
    int Total,
    int WeakCount,
    int ReusedCount,
    int AgedCount,
    IReadOnlyList<VaultHealthEntry> Items);

/// <summary>Computes the vault's password-health report. Requires the vault to be unlocked
/// (passwords are decrypted in-memory to detect weak/duplicate credentials).</summary>
public interface IVaultHealthService
{
    VaultHealthReport GetReport();
}
