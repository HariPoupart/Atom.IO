using AtomIO.Core;
using UnityEngine;

namespace AtomIO.Meta
{
    public enum EvolveResult
    {
        Success,
        Failed,
        NotEnoughEnergy,
        AlreadyMaxElement
    }

    /// <summary>
    /// Persistent progression: current element, banked energy, pity counter and tutorial flags.
    /// Stored locally with PlayerPrefs (offline game, no accounts).
    /// </summary>
    public static class MetaProgress
    {
        private const string KeyZ = "atomio.z";
        private const string KeyEnergy = "atomio.energy";
        private const string KeyFailures = "atomio.failures_at_tier";
        private const string KeyTutorial = "atomio.tutorial_stage";

        /// <summary>0 = first run (orbit damage), 1 = must evolve to He, 2 = throw / pickup lesson, 3 = done.</summary>
        public static int TutorialStage
        {
            get => PlayerPrefs.GetInt(KeyTutorial, 0);
            set { PlayerPrefs.SetInt(KeyTutorial, value); PlayerPrefs.Save(); }
        }

        public static int AtomicNumber => PlayerPrefs.GetInt(KeyZ, 1);

        public static int Energy => PlayerPrefs.GetInt(KeyEnergy, AtomFormulas.StarterEnergy);

        public static int FailuresAtTier => PlayerPrefs.GetInt(KeyFailures, 0);

        public static bool HasWon => AtomicNumber >= AtomFormulas.MaxAtomicNumber;

        public static int NextCost => AtomFormulas.EvolutionCost(AtomicNumber);

        public static float CurrentFailChance => AtomFormulas.FailChancePercent(AtomicNumber, FailuresAtTier);

        public static void BankRunEnergy(int amount)
        {
            PlayerPrefs.SetInt(KeyEnergy, Energy + Mathf.Max(0, amount));
            if (TutorialStage == 0)
            {
                TutorialStage = 1; // first death: lobby now forces the H to He evolution
            }

            PlayerPrefs.Save();
        }

        public static EvolveResult TryEvolve()
        {
            int z = AtomicNumber;
            if (z >= AtomFormulas.MaxAtomicNumber)
            {
                return EvolveResult.AlreadyMaxElement;
            }

            int cost = AtomFormulas.EvolutionCost(z);
            if (Energy < cost)
            {
                return EvolveResult.NotEnoughEnergy;
            }

            PlayerPrefs.SetInt(KeyEnergy, Energy - cost); // energy is spent either way (GDD 1.3.3)

            float roll = Random.Range(0f, 100f);
            bool failed = roll < AtomFormulas.FailChancePercent(z, FailuresAtTier);
            if (failed)
            {
                PlayerPrefs.SetInt(KeyFailures, FailuresAtTier + 1);
                PlayerPrefs.Save();
                return EvolveResult.Failed;
            }

            PlayerPrefs.SetInt(KeyZ, z + 1);
            PlayerPrefs.SetInt(KeyFailures, 0);
            if (TutorialStage == 1)
            {
                TutorialStage = 2;
            }

            PlayerPrefs.Save();
            return EvolveResult.Success;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyZ);
            PlayerPrefs.DeleteKey(KeyEnergy);
            PlayerPrefs.DeleteKey(KeyFailures);
            PlayerPrefs.DeleteKey(KeyTutorial);
            PlayerPrefs.Save();
        }
    }
}
