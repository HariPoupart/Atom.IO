using AtomIO.Core;
using UnityEngine;

namespace AtomIO.Meta
{
    /// <summary>
    /// Persistent progression: current element, energy bar progress and tutorial stage.
    /// Stored locally with PlayerPrefs (offline game, no accounts).
    /// GDD 1.3.3 (rescope): every bit of energy fills the bar; a full bar evolves the player
    /// to the next element and the bar empties to 0 (no carry-over, no cost, no failure).
    /// The bar keeps its progress between runs.
    /// </summary>
    public static class MetaProgress
    {
        private const string KeyZ = "atomio.v2.z";
        private const string KeyBar = "atomio.v2.bar";
        private const string KeyTutorial = "atomio.v2.tutorial_stage";

        /// <summary>0 = first run as H (orbit damage + orbs), 1 = just became He (throw / pickup lesson due), 2 = done.</summary>
        public static int TutorialStage
        {
            get => PlayerPrefs.GetInt(KeyTutorial, 0);
            set { PlayerPrefs.SetInt(KeyTutorial, value); PlayerPrefs.Save(); }
        }

        public static int AtomicNumber => Mathf.Clamp(PlayerPrefs.GetInt(KeyZ, 1), 1, AtomFormulas.MaxAtomicNumber);

        /// <summary>Energy currently in the bar.</summary>
        public static float BarEnergy => PlayerPrefs.GetFloat(KeyBar, 0f);

        /// <summary>Size of the bar at the current element (50 + 3Z); 0 at Neon.</summary>
        public static int BarCapacity => AtomFormulas.EnergyToEvolve(AtomicNumber);

        public static float BarFill01 => BarCapacity > 0 ? Mathf.Clamp01(BarEnergy / BarCapacity) : 1f;

        public static bool HasWon => AtomFormulas.IsFinalElement(AtomicNumber);

        /// <summary>
        /// Adds collected energy to the bar. Returns true when this filled the bar and the
        /// player evolved; the bar is then reset to 0 and any overflow is discarded.
        /// </summary>
        public static bool AddEnergy(float amount)
        {
            if (HasWon || amount <= 0f)
            {
                return false;
            }

            int z = AtomicNumber;
            float bar = BarEnergy + amount;
            if (bar < BarCapacity)
            {
                PlayerPrefs.SetFloat(KeyBar, bar);
                return false;
            }

            PlayerPrefs.SetInt(KeyZ, z + 1);
            PlayerPrefs.SetFloat(KeyBar, AtomFormulas.BarResetValue);
            if (z + 1 == 2 && TutorialStage == 0)
            {
                TutorialStage = 1; // first evolution: Helium unlocks the throw / pickup lesson
            }

            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Writes the bar to disk (called at run end; SetFloat alone is in memory).</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyZ);
            PlayerPrefs.DeleteKey(KeyBar);
            PlayerPrefs.DeleteKey(KeyTutorial);
            PlayerPrefs.Save();
        }
    }
}
