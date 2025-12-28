using System;
using System.Collections.Generic;

namespace CardGame.Core.Decks.Data
{
    public class DeckData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<int> CardIds { get; set; } = new List<int>();
    }
}