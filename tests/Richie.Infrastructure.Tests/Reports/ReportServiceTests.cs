using Richie.Application.Assets;
using Richie.Application.Reports;
using Richie.Application.Vault;
using Richie.Domain.Assets;
using Richie.Infrastructure.Assets;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Expenses;
using Richie.Infrastructure.Insurance;
using Richie.Infrastructure.Reports;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;
using Richie.Infrastructure.Vault;

namespace Richie.Infrastructure.Tests.Reports;

public sealed class ReportServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly VaultService _vault;
    private readonly ReportService _sut;

    public ReportServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        var assets = new AssetService(_db, new ValuationService(), _session, _clock);
        assets.Create(new AssetInput
        {
            Type = AssetType.Equity, Name = "Acme", InvestmentStartDate = _clock.UtcNow.AddYears(-1),
            InvestedAmount = 1000m, CurrentValue = 1200m, InvestmentMode = InvestmentMode.LumpSum
        });

        var gate = new VaultGate(_db, _session, new Pbkdf2KeyDerivation(), new AesGcmFieldCipher(),
            new Argon2PasswordHasher(), _clock);
        gate.SetupMasterPassword("master-pass-1");
        _vault = new VaultService(_db, _session, gate, _clock);
        _vault.Create(new VaultEntryInput("Gmail", "Email", null, "me@x.com", "S3cr3t!", null));

        new Richie.Infrastructure.Income.IncomeService(_db, _session, _clock)
            .Create(new Richie.Application.Income.IncomeInput(_clock.UtcNow, 5000m, "Salary", null));

        _sut = new ReportService(
            assets, new ValuationService(), new GoalService(_db, _session, _clock),
            new ExpenseService(_db, _session, _clock), new ExpenseAnalyticsService(_db, _session, _clock),
            new Richie.Infrastructure.Income.IncomeService(_db, _session, _clock),
            _vault, new InsuranceService(_db, _session, _clock), _clock);
    }

    [Fact]
    public void AssetsReport_HasSummarySection()
    {
        ReportContent report = _sut.Build(new ReportRequest(ReportType.Assets, null, null, false));

        Assert.Equal("Asset Report", report.Title);
        Assert.Contains(report.Sections, s => s.Heading == "Portfolio summary");
        Assert.Contains(report.Sections, s => s.Heading == "Allocation by type");
    }

    [Fact]
    public void VaultReport_MasksByDefault_AndRevealsWhenRequested()
    {
        ReportSection masked = _sut.Build(new ReportRequest(ReportType.Vault, null, null, false)).Sections.Single();
        string maskedPw = masked.Table!.Rows.Single()[3];
        Assert.DoesNotContain("S3cr3t!", maskedPw);

        ReportSection unmasked = _sut.Build(new ReportRequest(ReportType.Vault, null, null, true)).Sections.Single();
        Assert.Equal("S3cr3t!", unmasked.Table!.Rows.Single()[3]);
    }

    [Fact]
    public void AssetsReport_MoneyHasRupeePrefix_AndSummaryMarksSignedColumns()
    {
        ReportContent report = _sut.Build(new ReportRequest(ReportType.Assets, null, null, false));

        ReportSection summary = report.Sections.Single(s => s.Heading == "Portfolio summary");
        Assert.Equal([2, 3], summary.Table!.SignedColumns);
        Assert.All(summary.Table.Rows.Single().Take(3), v => Assert.StartsWith("₹", v)); // money cells
        Assert.EndsWith("%", summary.Table.Rows.Single()[3]);                              // return %

        ReportSection holdings = report.Sections.Single(s => s.Heading == "Holdings");
        Assert.Equal([4], holdings.Table!.SignedColumns);
    }

    [Fact]
    public void VaultReport_HasWebsiteColumnAndPerRowLinks()
    {
        _vault.Create(new VaultEntryInput("GitHub", "Dev", "https://github.com", "me", "p@ss", null));

        ReportSection vault = _sut.Build(new ReportRequest(ReportType.Vault, null, null, false)).Sections.Single();

        Assert.Equal(["Account", "Category", "User ID", "Password", "Website"], vault.Table!.Columns);
        Assert.Equal([0, 4], vault.Table.LinkColumns);
        Assert.Contains("https://github.com", vault.Table.RowLinks!);
    }

    [Fact]
    public void FullReport_CombinesAllModules()
    {
        ReportContent report = _sut.Build(new ReportRequest(ReportType.FullPortfolio, null, null, false));

        Assert.Contains(report.Sections, s => s.Heading == "Portfolio summary");
        Assert.Contains(report.Sections, s => s.Heading == "Expenses by category");
        Assert.Contains(report.Sections, s => s.Heading == "Insurance policies");
        Assert.Contains(report.Sections, s => s.Heading == "Vault accounts");
        Assert.Contains(report.Sections, s => s.Heading == "Income");
    }

    public void Dispose() => _db.Dispose();
}
