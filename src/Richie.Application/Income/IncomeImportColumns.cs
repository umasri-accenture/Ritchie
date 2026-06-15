namespace Richie.Application.Income;

/// <summary>Column names for the bulk income-upload template and parser.</summary>
public static class IncomeImportColumns
{
    public const string Date = "Date";
    public const string Amount = "Amount";
    public const string Source = "Source";
    public const string Notes = "Notes";

    public static readonly IReadOnlyList<string> All = [Date, Amount, Source, Notes];
}
