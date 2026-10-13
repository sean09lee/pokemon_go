using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class CacheServiceTests
{
    private sealed record SamplePayload(int Id, string Name);

    [Fact]
    public async Task SaveAsync_ThenTryGetAsync_RoundTripsData()
    {
        var cacheService = new CacheService();
        var cacheKey = $"test-cache-{Guid.NewGuid():N}";
        var payload = new SamplePayload(42, "Pikachu");

        try
        {
            await cacheService.SaveAsync(cacheKey, payload);
            var result = await cacheService.TryGetAsync<SamplePayload>(cacheKey);

            Assert.NotNull(result);
            Assert.Equal(payload.Id, result!.Id);
            Assert.Equal(payload.Name, result.Name);
        }
        finally
        {
            await CleanupCacheFileAsync(cacheService, cacheKey);
        }
    }

    [Fact]
    public async Task TryGetAsync_ReturnsDefault_WhenKeyDoesNotExist()
    {
        var cacheService = new CacheService();
        var cacheKey = $"missing-cache-{Guid.NewGuid():N}";

        var result = await cacheService.TryGetAsync<SamplePayload>(cacheKey);

        Assert.Null(result);
    }

    private static async Task CleanupCacheFileAsync(CacheService cacheService, string cacheKey)
    {
        var cacheDirectoryPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "TeamBuilder.Data", "Cache"));
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeKey = string.Concat(cacheKey.Select(c => invalidChars.Contains(c) ? '_' : char.ToLowerInvariant(c)));
        var filePath = Path.Combine(cacheDirectoryPath, $"{safeKey}.json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        await Task.CompletedTask;
    }
}
