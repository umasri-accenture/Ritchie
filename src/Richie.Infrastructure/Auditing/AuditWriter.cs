using Richie.Domain.Auditing;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Auditing;

/// <summary>
/// Adds an <see cref="AuditLog"/> entry to a context so it is saved in the same transaction
/// as the change it records. Reused by every module's write operations (CLAUDE.md).
/// </summary>
public static class AuditWriter
{
    public static void Add(
        RichieDbContext db, Guid? userId, DateTime nowUtc, string module,
        AuditAction action, string entityType, Guid entityId, string description)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TimestampUtc = nowUtc,
            Module = module,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description
        });
    }
}
