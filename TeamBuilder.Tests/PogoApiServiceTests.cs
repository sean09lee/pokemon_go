using System.Net;
using System.Text;
using TeamBuilder.Services.Helpers;
using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class PogoApiServiceTests
{
    [Fact]
    public async Task GetPokemonNamesAsync_ReturnsSuccessResponse_WhenRequestSucceeds()
    {
        var handler = new FakeJsonHandler(HttpStatusCode.OK, "[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pogoapi.net/") };
        var service = new PogoApiService(new PogoHelpers(httpClient));

        var response = await service.GetPokemonNamesAsync();

        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task GetPokemonNamesAsync_ReturnsFailureResponse_WhenRequestFails()
    {
        var handler = new FakeJsonHandler(HttpStatusCode.InternalServerError, "error");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pogoapi.net/") };
        var service = new PogoApiService(new PogoHelpers(httpClient));

        var response = await service.GetPokemonNamesAsync();

        Assert.False(response.IsSuccess);
    }

    [Fact]
    public async Task GetPokemonEvolutionsAsync_ParsesTypedJsonSuccessfully()
    {
        var json = "[{\"pokemon_id\":1,\"family_id\":1,\"candy_to_evolve\":25}]";
        var handler = new FakeJsonHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pogoapi.net/") };
        var service = new PogoApiService(new PogoHelpers(httpClient));

        var evolutions = await service.GetPokemonEvolutionsAsync();

        Assert.NotNull(evolutions);
    }

    private sealed class FakeJsonHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var message = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(message);
        }
    }
}
