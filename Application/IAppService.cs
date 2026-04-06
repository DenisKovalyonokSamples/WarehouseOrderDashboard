namespace Warehouse.Application.Services;

/// <summary>
/// Provides application-level status operations.
/// </summary>
public interface IAppService
{
    /// <summary>
    /// Returns a simple service health message.
    /// </summary>
    string GetMessage();
}
