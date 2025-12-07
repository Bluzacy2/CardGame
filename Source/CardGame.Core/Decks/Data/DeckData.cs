using System;
using System.Collections.Generic;

namespace CardGame.Core.Decks.Data
{
    public class DeckData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public List<int> CardIds { get; set; } = new List<int>();
    }
}
