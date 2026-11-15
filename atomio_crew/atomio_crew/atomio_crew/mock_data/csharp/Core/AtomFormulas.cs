namespace AtomIO.Core
{
    /// <summary>
    /// Fixed numbers from the Atom.IO GDD (sections 1.3.1 and 1.3.3, rescoped 2026-11-12).
    /// The game has 10 levels, Hydrogen (1) to Neon (10). Evolution has no cost and no failure
    /// chance: an energy bar of 50 + 3 x Z fills with collected energy, a full bar evolves the
    /// player, and the bar empties to 0. Tier JSON files carry the same values.
    /// </summary>
    public static class AtomFormulas
    {
        public const int MaxAtomicNumber = 10; // Neon: final element and win condition
        public const int HpPerProton = 10;
        public const int EnemyContactDamage = 10;
        public const int OrbitingElectronDamage = 10;
        public const int ReleasedElectronDamage = 20;
        public const int MinOrbitingElectrons = 0; // no minimum: the last electron can be thrown (2026-11-15)
        public const int BarBase = 50;
        public const int BarPerProton = 3;
        public const int BarResetValue = 0;

        public static int MaxHp(int z) => HpPerProton * z;

        public static int ElectronSlots(int z) => z;

        /// <summary>Energy needed to fill the bar at element z (50 + 3z). Neon has no bar: 0.</summary>
        public static int EnergyToEvolve(int z) => z >= MaxAtomicNumber ? 0 : BarBase + BarPerProton * z;

        public static bool IsFinalElement(int z) => z >= MaxAtomicNumber;
    }
}
