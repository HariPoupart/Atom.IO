namespace AtomIO.Core
{
    /// <summary>
    /// Fixed numbers from the Atom.IO GDD (sections 1.3.1 and 1.3.3, rescoped 2026-11-12).
    /// The game has 10 levels, Hydrogen (1) to Neon (10). Evolution has no cost and no failure
    /// chance: an energy bar of 50 + 3Z + 5(Z-1)^2 fills with collected energy, a full bar evolves the
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
        public const int BarCurve = 5;            // later levels need more energy: + 5 x (Z-1)^2
        public const float OrbEnergy = 2f;        // every energy orb holds this fixed amount
        public const int MaxOrbsPerKill = 15;     // cap on the orbs one enemy can drop

        public static int MaxHp(int z) => HpPerProton * z;

        public static int ElectronSlots(int z) => z;

        /// <summary>Energy needed to fill the bar at element z: 50 + 3z + 5(z-1)^2
        /// (53 at H, 61 He, 79 Li, 107 Be, 145 B, 193 C, 251 N, 319 O, 397 F). Neon has no bar: 0.</summary>
        public static int EnergyToEvolve(int z) =>
            z >= MaxAtomicNumber ? 0 : BarBase + BarPerProton * z + BarCurve * (z - 1) * (z - 1);

        public static bool IsFinalElement(int z) => z >= MaxAtomicNumber;
    }
}
