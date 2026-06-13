namespace Richie.Domain.Auditing;

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3
}

/// <summary>
/// An immutable record of a create/update/delete across any module (CLAUDE.md: every CUD is logged).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Module { get; set; } = string.Empty;     // e.g. "Assets"
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty; // e.g. "Asset"
    public Guid EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
}
