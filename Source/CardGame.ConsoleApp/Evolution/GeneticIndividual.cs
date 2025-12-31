using System.Text.Json.Serialization;

public class GeneticIndividual
{
    public string Id { get; set; }
    public float[] StrategyDNA { get; set; }
    public int[] DeckDNA { get; set; }
    public int Wins { get; set; }
    public int GamesPlayed { get; set; }
    public float Fitness { get; set; } // NOWE: Sumaryczna ocena bota
    public int Generation { get; set; }

    [JsonIgnore]
    public float WinRate => GamesPlayed == 0 ? 0 : (float)Wins / GamesPlayed;

    public GeneticIndividual() { Id = Guid.NewGuid().ToString().Substring(0, 8); }

    public GeneticIndividual(float[] strategyDNA, int[] deckDNA, int generation)
    {
        Id = Guid.NewGuid().ToString().Substring(0, 8);
        StrategyDNA = strategyDNA;
        DeckDNA = deckDNA;
        Generation = generation;
    }

    public GeneticIndividual Clone()
    {
        return new GeneticIndividual((float[])StrategyDNA.Clone(), (int[])DeckDNA.Clone(), Generation)
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8), // Nowy ID dla potomka
            Wins = 0,
            GamesPlayed = 0,
            Fitness = 0
        };
    }
}