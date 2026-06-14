using System.IO;
using System.Text;
using Richie.Application.Common;
using Richie.Application.Vault;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;
using Richie.Infrastructure.Vault;

namespace Richie.Infrastructure.Tests.Vault;

public sealed class VaultImportServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly VaultGate _gate;
    private readonly VaultService _vault;
    private readonly VaultImportService _sut;

    public VaultImportServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        _gate = new VaultGate(_db, _session, new Pbkdf2KeyDerivation(), new AesGcmFieldCipher(), _clock);
        _gate.SetupMasterPassword("master-pass-1");
        _vault = new VaultService(_db, _session, _gate, _clock);
        _sut = new VaultImportService(_vault);
    }

    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void ImportCsv_ImportsValidRows_AndReportsInvalid()
    {
        string csv = string.Join("\n",
            "AccountName,Category,Url,LoginId,Password,Notes",
            "HDFC Bank,Bank,https://hdfc.example,alice,S3cr3t!,main",
            ",Bank,,bob,pw123,no-name",          // missing AccountName
            "Gmail,Email,,carol,,no-pw");         // missing Password

        ImportResult result = _sut.ImportCsv(Csv(csv));

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Message.Contains("AccountName"));
        Assert.Contains(result.Errors, e => e.RowNumber == 4 && e.Message.Contains("Password"));

        VaultEntrySummary imported = Assert.Single(_vault.GetEntries());
        Assert.Equal("HDFC Bank", imported.AccountName);
        Assert.Equal("S3cr3t!", _vault.RevealPassword(imported.Id)); // encrypted on ingestion, round-trips
    }

    [Fact]
    public void ImportCsv_FlagsDuplicateOfExistingEntry()
    {
        _vault.Create(new VaultEntryInput("HDFC Bank", "Bank", null, "alice", "existing", null));

        string csv = string.Join("\n",
            "AccountName,Category,Url,LoginId,Password,Notes",
            "HDFC Bank,Bank,,alice,newpw,dup");   // same Account + User ID as the existing entry

        ImportResult result = _sut.ImportCsv(Csv(csv));

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
        Assert.Single(_vault.GetEntries());        // nothing added
    }

    [Fact]
    public void ImportCsv_FlagsDuplicateWithinTheFile()
    {
        string csv = string.Join("\n",
            "AccountName,Category,Url,LoginId,Password,Notes",
            "Gmail,Email,,carol,pw1,first",
            "Gmail,Email,,carol,pw2,second");      // same Account + User ID twice in the file

        ImportResult result = _sut.ImportCsv(Csv(csv));

        Assert.Equal(1, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
        Assert.Single(_vault.GetEntries());
    }

    [Fact]
    public void ImportExcel_RoundTripsThroughGeneratedTemplate()
    {
        byte[] template = _sut.CreateExcelTemplate();
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(template));
        var sheet = wb.Worksheet(1);
        sheet.Cell(2, 1).Value = "Netflix";    // AccountName
        sheet.Cell(2, 5).Value = "Stream123!"; // Password

        using var filled = new MemoryStream();
        wb.SaveAs(filled);
        filled.Position = 0;

        ImportResult result = _sut.ImportExcel(filled);

        Assert.Equal(1, result.ImportedCount);
        Assert.False(result.HasErrors);
        Assert.Equal("Netflix", _vault.GetEntries().Single().AccountName);
    }

    [Fact]
    public void CsvTemplate_HasAllColumns()
    {
        string text = Encoding.UTF8.GetString(_sut.CreateCsvTemplate());
        foreach (string column in VaultImportColumns.All)
            Assert.Contains(column, text);
    }

    public void Dispose() => _db.Dispose();
}
