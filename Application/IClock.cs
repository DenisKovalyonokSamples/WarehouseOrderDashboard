namespace Warehouse.Application.Services;

/// <summary>
/// Provides access to current UTC time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }
}

/// <summary>
/// Default clock implementation using system UTC time.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
