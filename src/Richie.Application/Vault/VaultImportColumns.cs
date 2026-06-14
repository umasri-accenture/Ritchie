namespace Richie.Application.Vault;

/// <summary>Column names for the bulk password-upload template and parser (PRD §8.4).</summary>
public static class VaultImportColumns
{
    public const string AccountName = "AccountName";
    public const string Category = "Category";
    public const string Url = "Url";
    public const string LoginId = "LoginId";
    public const string Password = "Password";
    public const string Notes = "Notes";

    public static readonly IReadOnlyList<string> All = [AccountName, Category, Url, LoginId, Password, Notes];
}
