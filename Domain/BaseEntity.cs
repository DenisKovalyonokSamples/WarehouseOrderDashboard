namespace Warehouse.Domain;

/// <summary>
/// Represents a shared base entity with auditing and optimistic concurrency fields.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC date and time when the entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency version.
    /// </summary>
    public long Version { get; set; }
}
