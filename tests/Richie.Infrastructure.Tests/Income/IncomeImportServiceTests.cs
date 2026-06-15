using System.IO;
using System.Text;
using Richie.Application.Common;
using Richie.Application.Income;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Income;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Income;

public sealed class IncomeImportServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly IncomeService _income;
    private readonly IncomeImportService _sut;

    public IncomeImportServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        _income = new IncomeService(_db, _session, _clock);
        _sut = new IncomeImportService(_income);
    }

    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void ImportCsv_ImportsValidRows_AndReportsInvalid()
    {
        string csv = string.Join("\n",
            "Date,Amount,Source,Notes",
            "2026-01-05,85000,Salary,Monthly salary",
            "2026-01-06,abc,Freelance,Bad amount",   // bad amount
            "2026-01-07,5000,,No source");           // missing source

        ImportResult result = _sut.ImportCsv(Csv(csv));

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Message.Contains("Amount"));
        Assert.Contains(result.Errors, e => e.RowNumber == 4 && e.Message.Contains("Source"));
        Assert.Single(_income.GetRecent());
    }

    [Fact]
    public void ImportExcel_RoundTripsThroughGeneratedTemplate()
    {
        byte[] template = _sut.CreateExcelTemplate();
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(template));
        var sheet = wb.Worksheet(1);
        sheet.Cell(2, 1).Value = "2026-01-10";   // Date
        sheet.Cell(2, 2).Value = 12000;          // Amount
        sheet.Cell(2, 3).Value = "Dividends";    // Source

        using var filled = new MemoryStream();
        wb.SaveAs(filled);
        filled.Position = 0;

        ImportResult result = _sut.ImportExcel(filled);

        Assert.Equal(1, result.ImportedCount);
        Assert.False(result.HasErrors);
        Assert.Equal(12000m, _income.GetRecent().Single().Amount);
    }

    [Fact]
    public void CsvTemplate_HasAllColumns()
    {
        string header = Encoding.UTF8.GetString(_sut.CreateCsvTemplate()).Trim();
        Assert.Equal(string.Join(",", IncomeImportColumns.All), header);
    }

    public void Dispose() => _db.Dispose();
}
