using TeamBuilder.Services.Services;

namespace TeamBuilder.Tests;

public class IndividualValueServiceTests
{
    [Fact]
    public void Calculate_GreatLeague_ReturnsRanksWithinCpLimit()
    {
        var result = IndividualValueService.Calculate(
            baseAtk: 118,
            baseDef: 111,
            baseSta: 128,
            floor: 0,
            minLvl: 1,
            maxLvl: 51,
            includeInvalids: false,
            league: 1500);

        Assert.True(result.NumRanks > 0);
        Assert.NotEmpty(result.SortedRanks);

        foreach (var bucket in result.SortedRanks)
        {
            foreach (var entry in bucket.Value)
            {
                Assert.True(entry.CP <= 1500, $"Entry CP {entry.CP} exceeds the 1500 league cap.");
            }
        }
    }

    [Fact]
    public void Calculate_RanksAreOrderedDescendingByStatProductKey()
    {
        var result = IndividualValueService.Calculate(
            baseAtk: 118,
            baseDef: 111,
            baseSta: 128,
            floor: 0,
            minLvl: 1,
            maxLvl: 51,
            includeInvalids: false,
            league: 1500);

        var keys = result.SortedRanks
            .Select(kvp => double.Parse(kvp.Key, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        var orderedDescending = keys.OrderByDescending(k => k).ToList();
        Assert.Equal(orderedDescending, keys);
    }

    [Fact]
    public void Calculate_NoLeagueCap_StillProducesRanks()
    {
        var result = IndividualValueService.Calculate(
            baseAtk: 118,
            baseDef: 111,
            baseSta: 128,
            floor: 15,
            minLvl: 1,
            maxLvl: 51,
            includeInvalids: false,
            league: 0);

        Assert.True(result.NumRanks > 0);
    }

    [Fact]
    public void Calculate_IncludeInvalids_CollectsEntriesExceedingLeagueCap()
    {
        var result = IndividualValueService.Calculate(
            baseAtk: 118,
            baseDef: 111,
            baseSta: 128,
            floor: 15,
            minLvl: 40,
            maxLvl: 51,
            includeInvalids: true,
            league: 10);

        Assert.NotEmpty(result.Invalids);
        Assert.All(result.Invalids, invalid => Assert.True(invalid.cp > 10));
    }
}
