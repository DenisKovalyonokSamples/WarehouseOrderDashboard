namespace Warehouse.Domain;

/// <summary>
/// Represents an audit entry for domain changes.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
