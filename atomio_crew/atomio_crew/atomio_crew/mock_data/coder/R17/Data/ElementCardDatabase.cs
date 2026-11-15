using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtomIO.Data
{
    /// <summary>One lobby card, as written by the content pipeline (element_cards.json).</summary>
    [Serializable]
    public class ElementCard
    {
        public int atomic_number;
        public string symbol;
        public string title;
        public string flavor;
    }

    [Serializable]
    public class ElementCardList
    {
        public ElementCard[] cards;
    }

    /// <summary>Loads Resources/AtomIO/Content/element_cards.json once and looks cards up by atomic number.</summary>
    public static class ElementCardDatabase
    {
        private const string ResourcePath = "AtomIO/Content/element_cards";
        private static Dictionary<int, ElementCard> byNumber;

        public static ElementCard ForElement(int atomicNumber)
        {
            Load();
            return byNumber.TryGetValue(atomicNumber, out var card) ? card : null;
        }

        private static void Load()
        {
            if (byNumber != null)
            {
                return;
            }

            byNumber = new Dictionary<int, ElementCard>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("[Atom.IO] element_cards.json not found in Resources/AtomIO/Content.");
                return;
            }

            var list = JsonUtility.FromJson<ElementCardList>(asset.text);
            if (list?.cards == null)
            {
                return;
            }

            foreach (var c in list.cards)
            {
                byNumber[c.atomic_number] = c;
            }
        }
    }
}
