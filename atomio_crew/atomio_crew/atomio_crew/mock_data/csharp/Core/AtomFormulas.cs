using UnityEngine;

namespace AtomIO.Core
{
    /// <summary>
    /// Fixed numbers from the Atom.IO GDD (sections 1.3.1 and 1.3.3).
    /// Tier JSON files carry the same values; this class is the in-code reference.
    /// </summary>
    public static class AtomFormulas
    {
        public const int MaxAtomicNumber = 118;
        public const int HpPerProton = 10;
        public const int EnemyContactDamage = 10;
        public const int OrbitingElectronDamage = 10;
        public const int ReleasedElectronDamage = 20;
        public const int MinOrbitingElectrons = 1;
        public const float PityStepPercent = 0.5f;

        public static int MaxHp(int z) => HpPerProton * z;

        public static int ElectronSlots(int z) => z;

        /// <summary>Energy needed to evolve from element z to z + 1.</summary>
        public static int EvolutionCost(int z) => 50 + 3 * z;

        /// <summary>(-1 + 1.04015^(z-1)) percent. 0 % for hydrogen, ~99 % at element 118.</summary>
        public static float BaseFailChancePercent(int z) => Mathf.Max(0f, -1f + Mathf.Pow(1.04015f, z - 1));

        /// <summary>Each failed attempt at the current tier lowers the chance by 0.5 %.</summary>
        public static float FailChancePercent(int z, int failuresAtTier) =>
            Mathf.Clamp(BaseFailChancePercent(z) - PityStepPercent * failuresAtTier, 0f, 100f);

        /// <summary>GDD 1.4.3: a new player can afford exactly one evolution (H to He).</summary>
        public static int StarterEnergy => EvolutionCost(1);
    }
}
