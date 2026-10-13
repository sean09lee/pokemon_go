using System.Text.Json;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.Services;

public class CacheService : ICacheService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private readonly string _cacheDirectoryPath;

	public CacheService()
	{
		_cacheDirectoryPath = ResolveCacheDirectoryPath();
		Directory.CreateDirectory(_cacheDirectoryPath);
	}

	public async Task<T?> TryGetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
	{
		try
		{
			var path = GetCacheFilePath(cacheKey);
			if (!File.Exists(path))
			{
				return default;
			}

			await using var stream = File.OpenRead(path);
			return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
		}
		catch
		{
			return default;
		}
	}

	public async Task SaveAsync<T>(string cacheKey, T data, CancellationToken cancellationToken = default)
	{
		var path = GetCacheFilePath(cacheKey);
		await using var stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
	}

	private static string ResolveCacheDirectoryPath()
	{
		var cwd = Directory.GetCurrentDirectory();
		return Path.GetFullPath(Path.Combine(cwd, "..", "TeamBuilder.Data", "Cache"));
	}

	private string GetCacheFilePath(string cacheKey)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		var safeKey = string.Concat(cacheKey.Select(c => invalidChars.Contains(c) ? '_' : char.ToLowerInvariant(c)));
		return Path.Combine(_cacheDirectoryPath, $"{safeKey}.json");
	}
}
