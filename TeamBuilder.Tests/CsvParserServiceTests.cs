using System.Text;
using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class CsvParserServiceTests
{
    [Fact]
    public async Task ParsePokemonCsvAsync_EmptyStream_ReturnsFatalError()
    {
        await using var stream = new MemoryStream();

        var result = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.False(string.IsNullOrWhiteSpace(result.FatalError));
        Assert.Empty(result.Pokemon);
    }

    [Fact]
    public async Task ParsePokemonCsvAsync_HeadersOnly_ReturnsNoPokemonAndNoFatalError()
    {
        var csv = "Index,Name,Form,Pokemon Number,CP,HP,Atk IV,Def IV,Sta IV,IV Avg,Level Min,Level Max\r\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await CsvParserService.ParsePokemonCsvAsync(stream);

        Assert.True(string.IsNullOrWhiteSpace(result.FatalError), result.FatalError);
        Assert.Empty(result.Pokemon);
    }

    [Fact]
    public async Task ParsePokemonCsvAsync_NullStream_ReturnsFatalError()
    {
        var result = await CsvParserService.ParsePokemonCsvAsync(null!);

        Assert.False(string.IsNullOrWhiteSpace(result.FatalError));
    }
}
