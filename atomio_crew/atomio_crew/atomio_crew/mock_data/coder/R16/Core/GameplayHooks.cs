using System;
using AtomIO.Combat;

namespace AtomIO.Core
{
    /// <summary>
    /// Small static hook points that existing gameplay scripts raise, so features such as the
    /// group abilities can react without the scripts knowing about them.
    /// </summary>
    public static class GameplayHooks
    {
        /// <summary>An orbiting electron hit an enemy.</summary>
        public static event Action<Enemy> OrbitHit;

        /// <summary>The player threw an electron.</summary>
        public static event Action<Electron> Released;

        /// <summary>An enemy was destroyed (its energy orb has just been dropped).</summary>
        public static event Action<Enemy> EnemyKilled;

        private static Electron boosted;
        private static float boostMult = 1f;

        public static void RaiseOrbitHit(Enemy enemy) => OrbitHit?.Invoke(enemy);

        public static void RaiseReleased(Electron electron) => Released?.Invoke(electron);

        public static void RaiseEnemyKilled(Enemy enemy) => EnemyKilled?.Invoke(enemy);

        /// <summary>The next hit from this thrown electron deals extra damage (alkali metals).</summary>
        public static void BoostNextHit(Electron electron, float mult)
        {
            boosted = electron;
            boostMult = mult;
        }

        /// <summary>Damage multiplier for a thrown electron's hit; a boost is used up by one hit.</summary>
        public static float DamageMultFor(Electron electron)
        {
            if (electron != null && electron == boosted)
            {
                boosted = null;
                return boostMult;
            }

            return 1f;
        }
    }
}
