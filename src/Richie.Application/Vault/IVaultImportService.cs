using Richie.Application.Common;

namespace Richie.Application.Vault;

/// <summary>Bulk import of credentials from CSV/Excel + template generation (PRD §8.4). Passwords are
/// encrypted on ingestion; rows duplicating an existing entry (Account + User ID) are flagged, not imported.
/// Requires the vault to be unlocked.</summary>
public interface IVaultImportService : IBulkImporter
{
}
