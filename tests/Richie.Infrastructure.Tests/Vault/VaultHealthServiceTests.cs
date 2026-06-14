using Richie.Application.Vault;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;
using Richie.Infrastructure.Vault;

namespace Richie.Infrastructure.Tests.Vault;

public sealed class VaultHealthServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly VaultGate _gate;
    private readonly VaultService _vault;
    private readonly VaultHealthService _sut;

    public VaultHealthServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        _gate = new VaultGate(_db, _session, new Pbkdf2KeyDerivation(), new AesGcmFieldCipher(), _clock);
        _gate.SetupMasterPassword("master-pass-1");
        _vault = new VaultService(_db, _session, _gate, _clock);
        _sut = new VaultHealthService(_db, _session, _gate, _clock);
    }

    private void Add(string account, string password) =>
        _vault.Create(new VaultEntryInput(account, "Bank", null, "user", password, null));

    [Fact]
    public void EmptyVault_ScoresFull_AndNoItems()
    {
        VaultHealthReport report = _sut.GetReport();
        Assert.Equal(100, report.Score);
        Assert.Equal("Good", report.Rating);
        Assert.Empty(report.Items);
    }

    [Fact]
    public void StrongUniqueRecent_HasNoIssues()
    {
        Add("A", "Abcdefghijk1!");
        Add("B", "Zyxwvuts9876#");

        VaultHealthReport report = _sut.GetReport();
        Assert.Equal(100, report.Score);
        Assert.Empty(report.Items);
        Assert.Equal(0, report.WeakCount);
        Assert.Equal(0, report.ReusedCount);
    }

    [Fact]
    public void WeakPassword_IsFlagged()
    {
        Add("Weak", "abc");                 // very weak
        Add("Strong", "Abcdefghijk1!");

        VaultHealthReport report = _sut.GetReport();
        Assert.Equal(1, report.WeakCount);
        VaultHealthEntry weak = Assert.Single(report.Items);
        Assert.Equal("Weak", weak.AccountName);
        Assert.Contains(weak.Issues, i => i.Contains("Weak"));
        Assert.True(report.Score < 100);
    }

    [Fact]
    public void ReusedPassword_FlagsBothEntries()
    {
        Add("Shared1", "Abcdefghijk1!");
        Add("Shared2", "Abcdefghijk1!");    // same strong password reused

        VaultHealthReport report = _sut.GetReport();
        Assert.Equal(2, report.ReusedCount);
        Assert.Equal(2, report.Items.Count);
        Assert.All(report.Items, i => Assert.Contains(i.Issues, s => s.Contains("Reused")));
    }

    [Fact]
    public void AgedPassword_IsFlagged_AfterThreshold()
    {
        Add("Old", "Abcdefghijk1!");

        Assert.Empty(_sut.GetReport().Items);   // fresh — no age issue

        _clock.Advance(TimeSpan.FromDays(200)); // past the 180-day mark
        VaultHealthReport report = _sut.GetReport();
        Assert.Equal(1, report.AgedCount);
        Assert.Contains(Assert.Single(report.Items).Issues, i => i.Contains("180"));
    }

    [Fact]
    public void Items_AreSortedWorstFirst()
    {
        Add("StrongOld", "Abcdefghijk1!");  // will be aged only (−20)
        Add("Weak", "abc");                 // weak (−40)
        _clock.Advance(TimeSpan.FromDays(200));

        VaultHealthReport report = _sut.GetReport();
        Assert.Equal("Weak", report.Items.First().AccountName);  // lowest score first
    }

    public void Dispose() => _db.Dispose();
}
