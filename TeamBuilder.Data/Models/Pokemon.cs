namespace TeamBuilder.Data.Models;

public class Pokemon
{
    public int? Index { get; set; }
    public string? Name { get; set; }
    public string? Form { get; set; }
    public int? PokemonNumber { get; set; }
    public string? Gender { get; set; }
    public int? CP { get; set; }
    public int? HP { get; set; }

    public int? AtkIV { get; set; }
    public int? DefIV { get; set; }
    public int? StaIV { get; set; }
    public decimal? IvAvg { get; set; }

    public decimal? LevelMin { get; set; }
    public decimal? LevelMax { get; set; }

    public string? QuickMove { get; set; }
    public string? ChargeMove { get; set; }
    public string? ChargeMove2 { get; set; }

    public DateTime? ScanDate { get; set; }
    public DateTime? OriginalScanDate { get; set; }
    public DateTime? CatchDate { get; set; }

    public string? Weight { get; set; }
    public string? Height { get; set; }

    public int? Lucky { get; set; }
    public int? ShadowPurified { get; set; }
    public int? Favorite { get; set; }
    public int? Dust { get; set; }

    public string? RankPercentG { get; set; }
    public int? RankNumberG { get; set; }
    public string? StatProdG { get; set; }
    public int? DustCostG { get; set; }
    public int? CandyCostG { get; set; }
    public string? NameG { get; set; }
    public string? FormG { get; set; }
    public int? ShaPurG { get; set; }

    public string? RankPercentU { get; set; }
    public int? RankNumberU { get; set; }
    public string? StatProdU { get; set; }
    public int? DustCostU { get; set; }
    public int? CandyCostU { get; set; }
    public string? NameU { get; set; }
    public string? FormU { get; set; }
    public int? ShaPurU { get; set; }

    public string? RankPercentL { get; set; }
    public int? RankNumberL { get; set; }
    public string? StatProdL { get; set; }
    public int? DustCostL { get; set; }
    public int? CandyCostL { get; set; }
    public string? NameL { get; set; }
    public string? FormL { get; set; }
    public int? ShaPurL { get; set; }

    public string? MarkedForPvpUse { get; set; }
}