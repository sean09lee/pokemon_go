using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class PvpApiServiceTests
{
    [Fact]
    public async Task GetPvpData_ReturnsEmptyString()
    {
        var service = new PvpApiService();

        var result = await service.GetPvpData();

        Assert.Equal(string.Empty, result);
    }
}
