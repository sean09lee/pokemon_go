using System.Text.Json;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.Services.Helpers;

public class PogoHelpers(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal async Task<PogoApiResponse> GetJsonAsync(string endpointFileName, CancellationToken cancellationToken)
    {
        try
        {
            var path = BuildPath(endpointFileName);
            using var response = await _httpClient.GetAsync(path, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return PogoApiResponse.Fail(endpointFileName, $"Request failed with status {(int)response.StatusCode}.", (int)response.StatusCode, content);
            }

            using var json = JsonDocument.Parse(content);
            return PogoApiResponse.Success(endpointFileName, json.RootElement.Clone(), content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return PogoApiResponse.Fail(endpointFileName, ex.Message);
        }
    }

    internal async Task<T?> GetTypedJsonAsync<T>(string endpointFileName, CancellationToken cancellationToken)
    {
        try
        {
            var path = BuildPath(endpointFileName);
            using var response = await _httpClient.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return default;
        }
    }

    internal static string BuildPath(string endpointFileName)
    {
        endpointFileName = endpointFileName.TrimStart('/');
        return endpointFileName.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase)
          ? endpointFileName
          : $"api/v1/{endpointFileName}";
    }
}
