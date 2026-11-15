using System;
using System.Collections;
using AtomIO.Arena;
using AtomIO.Combat;
using AtomIO.Core;
using AtomIO.Data;
using AtomIO.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomIO.Abilities
{
    /// <summary>
    /// Runs the current element group's ability (GDD 1.4.5) in the arena. It attaches itself to the
    /// ArenaSession at runtime (no scene wiring), follows the level's group and switches ability when
    /// the player evolves into a new group. Triggers: passive, on_low_hp, on_kill, on_pickup,
    /// on_orbit_hit, on_release. Effects: chain_hit, shield_seconds, damage_mult, heal_hp,
    /// pickup_radius_mult, energy_mult.
    /// </summary>
    public class GroupAbilityController : MonoBehaviour
    {
        private const float LowHpFraction = 0.3f;   // "below 30% HP" in the ability texts
        private const int ChainDamage = 10;          // same as an orbiting electron hit (GDD 1.3.1)
        private const float ChainRange = 4f;
        private const float BasePickupRadius = 1.5f;
        private const float MagnetSpeed = 9f;

        public static GroupAbilityController Instance { get; private set; }

        public GroupAbility Current { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool IsReady => Current != null && Time.time >= readyAt;

        public event Action<GroupAbility> AbilityChanged;
        public event Action<GroupAbility> Triggered;

        private ArenaSession session;
        private PlayerAtom player;
        private float readyAt;
        private bool lowHpArmed = true;
        private float nextMagnetScan;
        private EnergyOrb[] nearbyOrbs = new EnergyOrb[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            var arena = ArenaSession.Instance != null ? ArenaSession.Instance : FindFirstObjectByType<ArenaSession>();
            if (arena != null && arena.GetComponent<GroupAbilityController>() == null)
            {
                arena.gameObject.AddComponent<GroupAbilityController>();
            }
        }

        private void Awake()
        {
            Instance = this;
            session = GetComponent<ArenaSession>();
        }

        private IEnumerator Start()
        {
            while (session != null && session.Config == null)
            {
                yield return null; // ArenaSession loads the level in its own Start
            }

            if (session == null)
            {
                yield break;
            }

            player = session.Player;
            session.Evolved += Apply;
            session.BarChanged += OnBarChanged;
            if (player != null)
            {
                player.HealthChanged += OnHealthChanged;
            }

            GameplayHooks.OrbitHit += OnOrbitHit;
            GameplayHooks.Released += OnReleased;
            GameplayHooks.EnemyKilled += OnEnemyKilled;
            Apply(session.Config);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (session != null)
            {
                session.Evolved -= Apply;
                session.BarChanged -= OnBarChanged;
            }

            if (player != null)
            {
                player.HealthChanged -= OnHealthChanged;
            }

            GameplayHooks.OrbitHit -= OnOrbitHit;
            GameplayHooks.Released -= OnReleased;
            GameplayHooks.EnemyKilled -= OnEnemyKilled;
        }

        private void Apply(TierConfig config)
        {
            var next = config != null ? GroupAbilityDatabase.ForGroup(config.group) : null;
            if (next == Current)
            {
                return;
            }

            Current = next;
            readyAt = Time.time;
            lowHpArmed = true;
            AbilityChanged?.Invoke(Current);
        }

        // ------------------------------------------------------------------ triggers
        private void Update()
        {
            if (Current == null || Current.trigger != "passive" || Current.effect != "pickup_radius_mult" || player == null)
            {
                return;
            }

            // Passive magnet: orbs inside the enlarged radius drift toward the core.
            if (Time.time >= nextMagnetScan)
            {
                nextMagnetScan = Time.time + 0.25f;
                nearbyOrbs = FindObjectsByType<EnergyOrb>(FindObjectsSortMode.None);
            }

            float radius = BasePickupRadius * player.transform.localScale.x * Mathf.Max(1f, Current.value);
            Vector3 target = player.transform.position;
            foreach (var orb in nearbyOrbs)
            {
                if (orb != null && (orb.transform.position - target).sqrMagnitude <= radius * radius)
                {
                    orb.transform.position = Vector3.MoveTowards(orb.transform.position, target, MagnetSpeed * Time.deltaTime);
                }
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            if (Current == null || Current.trigger != "on_low_hp" || max <= 0)
            {
                return;
            }

            bool low = current > 0 && current <= Mathf.CeilToInt(max * LowHpFraction);
            if (!low)
            {
                lowHpArmed = true; // re-arm once the player is healthy again
                return;
            }

            if (lowHpArmed && TryUse())
            {
                lowHpArmed = false;
                ApplyEffect(null, null);
            }
        }

        private void OnBarChanged(float energy, int capacity)
        {
            if (Current != null && Current.trigger == "on_pickup" && TryUse())
            {
                ApplyEffect(null, null);
            }
        }

        private void OnOrbitHit(Enemy enemy)
        {
            if (Current != null && Current.trigger == "on_orbit_hit" && TryUse())
            {
                ApplyEffect(enemy, null);
            }
        }

        private void OnReleased(Electron electron)
        {
            if (Current != null && Current.trigger == "on_release" && TryUse())
            {
                ApplyEffect(null, electron);
            }
        }

        private void OnEnemyKilled(Enemy enemy)
        {
            if (Current != null && Current.trigger == "on_kill" && enemy != null && TryUse())
            {
                ApplyEffect(enemy, null);
            }
        }

        private bool TryUse()
        {
            if (!IsReady || session == null || !session.Running)
            {
                return false;
            }

            readyAt = Time.time + Mathf.Max(0f, Current.cooldown_s);
            return true;
        }

        // ------------------------------------------------------------------ effects
        private void ApplyEffect(Enemy enemy, Electron electron)
        {
            switch (Current.effect)
            {
                case "heal_hp":
                    if (player != null)
                    {
                        player.Heal(Mathf.RoundToInt(Current.value));
                    }

                    break;
                case "shield_seconds":
                    if (player != null)
                    {
                        player.Shield(Current.value);
                    }

                    break;
                case "damage_mult":
                    GameplayHooks.BoostNextHit(electron, Current.value);
                    break;
                case "chain_hit":
                    ChainFrom(enemy, Mathf.RoundToInt(Current.value));
                    break;
                case "energy_mult":
                    StartCoroutine(BoostDroppedOrb(enemy.transform.position, Current.value));
                    break;
                default:
                    return; // passive effects are handled in Update
            }

            Triggered?.Invoke(Current);
        }

        private void ChainFrom(Enemy source, int count)
        {
            if (source == null || count <= 0)
            {
                return;
            }

            var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            Vector3 origin = source.transform.position;
            for (int hit = 0; hit < count; hit++)
            {
                Enemy best = null;
                float bestDist = ChainRange * ChainRange;
                foreach (var e in enemies)
                {
                    if (e == null || e == source || e.Hp <= 0f)
                    {
                        continue;
                    }

                    float d = (e.transform.position - origin).sqrMagnitude;
                    if (d < bestDist)
                    {
                        best = e;
                        bestDist = d;
                    }
                }

                if (best == null)
                {
                    return;
                }

                best.TakeDamage(ChainDamage);
                source = best;
                origin = best.transform.position;
            }
        }

        private IEnumerator BoostDroppedOrb(Vector3 at, float mult)
        {
            yield return null; // the orb is spawned in the same frame the enemy dies
            EnergyOrb closest = null;
            float bestDist = 0.75f * 0.75f;
            foreach (var orb in FindObjectsByType<EnergyOrb>(FindObjectsSortMode.None))
            {
                float d = (orb.transform.position - at).sqrMagnitude;
                if (d < bestDist)
                {
                    closest = orb;
                    bestDist = d;
                }
            }

            if (closest != null)
            {
                closest.SetValue(closest.Value * mult);
            }
        }
    }
}
