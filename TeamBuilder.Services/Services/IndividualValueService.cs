using System.Diagnostics;
using System.Globalization;

namespace TeamBuilder.Services.Services;

public static class IndividualValueService
{
    /* cpm array used for calculations, source: https://www.reddit.com/r/TheSilphRoad/comments/jwjbw4/level_4550_expected_cpms_based/ */
    private static readonly double[] Cpm =
    {
        0.0939999967813491, 0.135137430784308, 0.166397869586944, 0.192650914456886,
        0.215732470154762, 0.236572655026622, 0.255720049142837, 0.273530381100769,
        0.29024988412857, 0.306057381335773, 0.321087598800659, 0.335445032295077,
        0.349212676286697, 0.36245774877879, 0.375235587358474, 0.387592411085168,
        0.399567276239395, 0.41119354951725, 0.422500014305114, 0.432926413410414,
        0.443107545375824, 0.453059953871985, 0.46279838681221, 0.472336077786704,
        0.481684952974319, 0.490855810259008, 0.499858438968658, 0.508701756943992,
        0.517393946647644, 0.525942508771329, 0.534354329109191, 0.542635762230353,
        0.550792694091796, 0.558830599438087, 0.566754519939422, 0.574569148039264,
        0.582278907299041, 0.589887911977272, 0.59740000963211, 0.604823657502073,
        0.61215728521347, 0.61940411056605, 0.626567125320434, 0.633649181622743,
        0.640652954578399, 0.647580963301656, 0.654435634613037, 0.661219263506722,
        0.667934000492096, 0.674581899290818, 0.681164920330047, 0.687684905887771,
        0.694143652915954, 0.700542893277978, 0.706884205341339, 0.713169102333341,
        0.719399094581604, 0.725575616972598, 0.731700003147125, 0.734741011137376,
        0.737769484519958, 0.740785574597326, 0.743789434432983, 0.746781208702482,
        0.749761044979095, 0.752729105305821, 0.75568550825119, 0.758630366519684,
        0.761563837528228, 0.764486065255226, 0.767397165298461, 0.77029727397159,
        0.77318650484085, 0.776064945942412, 0.778932750225067, 0.781790064808426,
        0.784636974334716, 0.787473583646825, 0.790300011634826, 0.792803950958807,
        0.795300006866455, 0.79780392148697, 0.800300002098083, 0.802803892322847,
        0.805299997329711, 0.807803863460723, 0.81029999256134, 0.812803834895026,
        0.815299987792968, 0.817803806620319, 0.820299983024597, 0.822803778631297,
        0.825299978256225, 0.827803750922782, 0.830299973487854, 0.832803753381377,
        0.835300028324127, 0.837803755931569, 0.840300023555755, 0.842803729034748,
        0.845300018787384, 0.847803702398935, 0.850300014019012, 0.852803676019539,
        0.85530000925064, 0.857803649892077, 0.860300004482269, 0.862803624012168,
        0.865299999713897,
    };

    public static CalculationResult Calculate(
        int baseAtk,
        int baseDef,
        int baseSta,
        int floor,
        double minLvl,
        double maxLvl,
        bool includeInvalids,
        int league,
        string mon = "",
        bool perfTiming = false)
    {
        var ranks = new Dictionary<string, List<RankEntry>>();
        var invalids = new List<InvalidIvEntry>();

        var maxAtk = new StatExtremum(0, 0, 0, 0, 0);
        var maxDef = new StatExtremum(0, 0, 0, 0, 0);
        var maxHP = new StatExtremum(0, 0, 0, 0, 0);
        var minAtk = new StatExtremum(1000, 0, 0, 0, 0);
        var minDef = new StatExtremum(1000, 0, 0, 0, 0);
        var minHP = new StatExtremum(1000, 0, 0, 0, 0);

        var minRankLvl = 100;
        var maxRankLvl = 0;
        var numRanks = 0;

        // Account for half-level CPMs (40-1)*2 = 78
        var minLvlIndex = (int)Math.Max(0, (minLvl - 1) * 2); // use half-levels
        var maxLvlIndex = (int)Math.Max(0, (maxLvl - 1) * 2); // use half-levels

        long buildMs = 0;
        long sortMs = 0;
        var stopwatch = new Stopwatch();

        if (perfTiming)
        {
            stopwatch.Start();
        }

        for (var atk = floor; atk <= 15; atk++)
        {
            for (var def = floor; def <= 15; def++)
            {
                for (var sta = floor; sta <= 15; sta++)
                {
                    var solvedCpm = Math.Sqrt(
                        (10d * league)
                        / ((baseAtk + atk)
                           * Math.Sqrt(baseDef + def)
                           * Math.Sqrt(baseSta + sta)));

                    var testLvl = FindFirstGreaterThan(Cpm, solvedCpm);
                    if (testLvl > -1)
                    {
                        testLvl -= 1;
                    }
                    else
                    {
                        testLvl = 100; // maxLvl case
                    }

                    for (var level = maxLvlIndex; level >= minLvlIndex; level--)
                    {
                        var cp = Math.Max(
                            10,
                            (int)Math.Floor(
                                ((baseAtk + atk)
                                 * Math.Sqrt(baseDef + def)
                                 * Math.Sqrt(baseSta + sta)
                                 * Cpm[level]
                                 * Cpm[level])
                                / 10));

                        if (league != 0 && cp > league)
                        {
                            if (level == minLvlIndex && includeInvalids)
                            {
                                invalids.Add(new InvalidIvEntry(atk, def, sta, cp));
                            }

                            continue; // keep searching for level
                        }

                        // Update maxLvlIndex on first loop (0/0/0 or floor/floor/floor) to optimize performance
                        if (atk == floor && def == floor && sta == floor)
                        {
                            maxLvlIndex = level;
                        }

                        if (testLvl != level)
                        {
                            Console.Error.WriteLine(
                                $"Mismatch! testLvl:{testLvl}, level:{level} for {atk}/{def}/{sta}, cp:{cp}");
                        }

                        var aSt = (baseAtk + atk) * Cpm[level];
                        var dSt = (baseDef + def) * Cpm[level];
                        var sSt = (int)Math.Floor((baseSta + sta) * Cpm[level]);
                        var statProd = (int)Math.Round(aSt * dSt * sSt);

                        // update max stats if necessary
                        if (maxAtk.Value < aSt || (maxAtk.SP < statProd && maxAtk.Value <= aSt))
                        {
                            maxAtk = maxAtk with { Value = aSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (maxDef.Value < dSt || (maxDef.SP < statProd && maxDef.Value <= dSt))
                        {
                            maxDef = maxDef with { Value = dSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (maxHP.Value < sSt || (maxHP.SP < statProd && maxHP.Value <= sSt))
                        {
                            maxHP = maxHP with { Value = sSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (level > maxRankLvl)
                        {
                            maxRankLvl = level;
                        }

                        // update min stats if necessary
                        if (minAtk.Value > aSt || (minAtk.SP < statProd && minAtk.Value >= aSt))
                        {
                            minAtk = minAtk with { Value = aSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (minDef.Value > dSt || (minDef.SP < statProd && minDef.Value >= dSt))
                        {
                            minDef = minDef with { Value = dSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (minHP.Value > sSt || (minHP.SP < statProd && minHP.Value >= sSt))
                        {
                            minHP = minHP with { Value = sSt, AIV = atk, DIV = def, SIV = sta, SP = statProd };
                        }

                        if (level < minRankLvl)
                        {
                            minRankLvl = level;
                        }

                        var ivSum = atk + def + sta;
                        var star = ivSum switch
                        {
                            < 23 => "0*",
                            < 30 => "1*",
                            < 37 => "2*",
                            < 45 => "3*",
                            _ => "4*"
                        };

                        var outputLevel = level / 2d + 1;

                        // store as arrays to prevent hash collisions from dropping entries
                        // Tie Breaking Order: 1)StatProd -> 2)AtkStat -> 3)HPval -> 4)finalCP -> 5)StaIV -> 6)ERROR
                        var newIndex = statProd + "." + Math.Round(100000 * aSt, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

                        var newEntry = new RankEntry(
                            new IvStats(atk, def, sta, star),
                            new BattleStats(aSt, dSt, sSt),
                            outputLevel,
                            cp);

                        if (!ranks.TryGetValue(newIndex, out var bucket))
                        {
                            ranks[newIndex] = new List<RankEntry> { newEntry };
                        }
                        else
                        {
                            var insertAt = bucket.Count;
                            for (var i = 0; i < bucket.Count; i++)
                            {
                                if (sSt > bucket[i].Battle.S)
                                {
                                    insertAt = i;
                                    break;
                                }

                                if (sSt == bucket[i].Battle.S)
                                {
                                    if (cp > bucket[i].CP)
                                    {
                                        insertAt = i;
                                        break;
                                    }

                                    if (cp == bucket[i].CP)
                                    {
                                        if (sta > bucket[i].IVs.S)
                                        {
                                            insertAt = i;
                                            break;
                                        }

                                        if (sta == bucket[i].IVs.S)
                                        {
                                            Console.WriteLine(
                                                $"Need 6th tie breaker for newIndex({newIndex}):{System.Text.Json.JsonSerializer.Serialize(bucket)}");
                                        }
                                    }
                                }
                            }

                            bucket.Insert(insertAt, newEntry);
                        }

                        numRanks++;
                        break; // stop evaluating this IV combination
                    }
                }
            }
        }

        if (perfTiming)
        {
            buildMs = stopwatch.ElapsedMilliseconds;
        }

        // sort by statProd+CP before returning
        var orderedRanks = ranks
            .OrderByDescending(kvp => double.Parse(kvp.Key, CultureInfo.InvariantCulture))
            .ToList();

        if (perfTiming)
        {
            sortMs = stopwatch.ElapsedMilliseconds - buildMs;
        }

        var result = new CalculationResult
        {
            SortedRanks = orderedRanks,
            MaxAtk = maxAtk,
            MaxDef = maxDef,
            MaxHP = maxHP,
            MinAtk = minAtk,
            MinDef = minDef,
            MinHP = minHP,
            MinLvl = 1 + minRankLvl / 2d,
            MaxLvl = 1 + maxRankLvl / 2d,
            NumRanks = numRanks,
            Invalids = invalids
        };

        if (perfTiming)
        {
            var totalMs = buildMs + sortMs;
            result.TimingSummary =
                $"{mon} calculateFancy: {totalMs:F1}ms (Build {buildMs:F1}ms + Sort {sortMs:F1}ms) for {numRanks} ranks.";
            Console.WriteLine(result.TimingSummary);
        }

        return result;
    }

    private static int FindFirstGreaterThan(double[] values, double threshold)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > threshold)
            {
                return i;
            }
        }

        return -1;
    }
}

public sealed record IvStats(int A, int D, int S, string Star);
public sealed record BattleStats(double A, double D, int S);
public sealed record RankEntry(IvStats IVs, BattleStats Battle, double L, int CP);
public sealed record InvalidIvEntry(int A, int D, int S, int cp);
public sealed record StatExtremum(double Value, int AIV, int DIV, int SIV, int SP);

public sealed class CalculationResult
{
    public List<KeyValuePair<string, List<RankEntry>>> SortedRanks { get; init; } = new();
    public StatExtremum MaxAtk { get; init; } = new(0, 0, 0, 0, 0);
    public StatExtremum MaxDef { get; init; } = new(0, 0, 0, 0, 0);
    public StatExtremum MaxHP { get; init; } = new(0, 0, 0, 0, 0);
    public StatExtremum MinAtk { get; init; } = new(0, 0, 0, 0, 0);
    public StatExtremum MinDef { get; init; } = new(0, 0, 0, 0, 0);
    public StatExtremum MinHP { get; init; } = new(0, 0, 0, 0, 0);
    public double MinLvl { get; init; }
    public double MaxLvl { get; init; }
    public int NumRanks { get; init; }
    public List<InvalidIvEntry> Invalids { get; init; } = new();
    public string? TimingSummary { get; set; }
}
