using Xunit;
using Warehouse.Application.Services;

namespace Warehouse.Tests;

public class WarehouseOrderTests
{
    [Fact]
    public void GetMessage_ReturnsRunning()
    {
        IAppService service = new AppService();
        Assert.Equal("Running", service.GetMessage());
    }
}
