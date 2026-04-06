namespace Warehouse.Application.Services;

/// <summary>
/// Default implementation of basic application service operations.
/// </summary>
public sealed class AppService : IAppService
{
    /// <inheritdoc />
    public string GetMessage() => "Running";
}
