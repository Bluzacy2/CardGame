using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardGame.ConsoleApp.Evolution
{
    public class EvolutionLeague
    {
        private readonly List<GeneticIndividual> _legends = new();
        private readonly string _hofPath = "EvolutionData/hall_of_fame";

        public EvolutionLeague()
        {
            if (!Directory.Exists(_hofPath)) Directory.CreateDirectory(_hofPath);
            LoadLegends();
        }

        public void AddChampion(GeneticIndividual champion)
        {
            // Dodaj do listy i zapisz na dysku
            _legends.Add(champion.Clone());
            string fileName = $"{_hofPath}/gen_{champion.Generation}_{champion.Id}.json";
            File.WriteAllText(fileName, JsonSerializer.Serialize(champion));

            // Zachowaj tylko 20 najciekawszych legend (np. co 5 generacji lub najsilniejsze)
            if (_legends.Count > 20) _legends.RemoveAt(0);
        }

        public GeneticIndividual GetRandomLegend()
        {
            if (!_legends.Any()) return null;
            return _legends[new System.Random().Next(_legends.Count)];
        }

        private void LoadLegends()
        {
            var files = Directory.GetFiles(_hofPath, "*.json");
            foreach (var file in files)
            {
                var legend = JsonSerializer.Deserialize<GeneticIndividual>(File.ReadAllText(file));
                if (legend != null) _legends.Add(legend);
            }
        }
    }
}