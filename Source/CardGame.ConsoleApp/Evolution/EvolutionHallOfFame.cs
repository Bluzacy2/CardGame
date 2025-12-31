using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardGame.Core.Cards.Data;

namespace CardGame.ConsoleApp.Evolution
{
    public class HallOfFame
    {
        private const string FilePath = "hall_of_fame.json";
        public List<GeneticIndividual> Champions { get; private set; } = new();
        private const float SimilarityLimit = 0.85f;

        public void ProcessPopulation(List<GeneticIndividual> currentGenSorted)
        {
            Load();
            if (Champions.Count == 0) InitializeEmptySlots();

            var top50 = currentGenSorted.Take(50).ToList();

            // Slot 0: APEX
            UpdateSlot(0, top50[0]);

            // Slot 1: AGGRO
            var bestAggro = top50.OrderByDescending(c => GetSafeDNA(c, DNA.Aggro_Bias) * c.Fitness).First();
            UpdateSlot(1, bestAggro);

            // Slot 2: CONTROL
            var bestControl = top50.OrderByDescending(c => GetSafeDNA(c, DNA.Control_Bias) * c.Fitness).First();
            UpdateSlot(2, bestControl);

            // Slot 3: COMBO
            var bestCombo = top50.OrderByDescending(c => GetSafeDNA(c, DNA.Combo_Bias) * (1 + c.Fitness)).First();
            UpdateSlot(3, bestCombo);

            // Slot 4: OUTLIER
            var outlier = top50.OrderByDescending(c => CalculateDistanceToGroup(c, Champions)).First();
            UpdateSlot(4, outlier);

            Save();
        }

        private float GetSafeDNA(GeneticIndividual bot, int index)
        {
            if (bot.StrategyDNA == null || index >= bot.StrategyDNA.Length) return 0f;
            return bot.StrategyDNA[index];
        }

        private void UpdateSlot(int slotIndex, GeneticIndividual candidate)
        {
            if (Champions[slotIndex] == null || candidate.Fitness > Champions[slotIndex].Fitness)
            {
                if (slotIndex > 0)
                {
                    for (int i = 0; i < slotIndex; i++)
                    {
                        if (CalculateSimilarity(candidate, Champions[i]) > SimilarityLimit) return;
                    }
                }
                Champions[slotIndex] = candidate;
            }
        }

        private void InitializeEmptySlots() { while (Champions.Count < 5) Champions.Add(null); }

        private float CalculateDistanceToGroup(GeneticIndividual bot, List<GeneticIndividual> group)
        {
            var valid = group.Where(g => g != null).ToList();
            if (!valid.Any()) return 1.0f;
            return valid.Average(g => 1.0f - CalculateSimilarity(bot, g));
        }

        private float CalculateSimilarity(GeneticIndividual a, GeneticIndividual b)
        {
            if (a == null || b == null) return 0;
            var setA = a.DeckDNA.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            var setB = b.DeckDNA.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            float shared = 0;
            foreach (var id in setA.Keys) if (setB.ContainsKey(id)) shared += Math.Min(setA[id], setB[id]) * 2;
            float deckSim = shared / 60.0f;

            float dnaDiff = 0;
            int[] keyGenes = { DNA.Aggro_Bias, DNA.Control_Bias, DNA.MonsterAffinity, DNA.AdjacencyBonus };
            foreach (int i in keyGenes) dnaDiff += Math.Abs(GetSafeDNA(a, i) - GetSafeDNA(b, i));
            float dnaSim = 1.0f - Math.Clamp(dnaDiff / 20.0f, 0, 1);

            return (deckSim * 0.6f) + (dnaSim * 0.4f);
        }

        public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(Champions.Where(c => c != null).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        public void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<List<GeneticIndividual>>(File.ReadAllText(FilePath));
                    Champions = loaded ?? new List<GeneticIndividual>();
                }
                catch { Champions = new List<GeneticIndividual>(); }
            }
            while (Champions.Count < 5) Champions.Add(null);
        }
    }
}