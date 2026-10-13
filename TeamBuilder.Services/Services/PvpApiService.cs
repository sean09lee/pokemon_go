using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.Services;

public class PvpApiService : IPvpApiService
{
    public Task<string> GetPvpData()
    {
        return Task.FromResult(string.Empty);
    }
}
