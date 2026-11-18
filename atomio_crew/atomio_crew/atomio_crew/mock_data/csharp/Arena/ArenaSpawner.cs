using System.Collections.Generic;
using AtomIO.Combat;
using AtomIO.Data;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Spawns enemies, energy orbs and wild electrons. Intervals shrink with time in the
    /// arena (interval = base / (1 + k * minutes)), all values come from the tier config.
    /// </summary>
    public class ArenaSpawner : MonoBehaviour
    {
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnergyOrb orbPrefab;
        [SerializeField] private ElectronPool electronPool;
        [SerializeField] private Electron electronPrefab; // fallback when there is no pool
        [SerializeField] private float minEnemyDistanceFromPlayer = 12f;
        [SerializeField] private int maxIdleOrbs = 40;

        [Header("Difficulty ramp (target: an average run lasts about a minute)")]
        [Tooltip("Seconds before the first enemy appears.")]
        [SerializeField] private float startGraceSeconds = 1.5f;
        [Tooltip("The level's max enemies alive grows by this fraction per minute survived (1 = doubles after 1 min).")]
        [SerializeField] private float aliveCapGrowthPerMinute = 1f;
        [Tooltip("New enemies are this much faster per minute survived (0.4 = +40% after 1 min).")]
        [SerializeField] private float enemySpeedGrowthPerMinute = 0.4f;
        [SerializeField] private float maxEnemySpeedMultiplier = 1.8f;

        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Electron> wildElectrons = new List<Electron>();
        private ArenaSession session;
        private TierConfig config;
        private float nextEnemy;
        private float nextOrb;
        private float nextElectron;
        private bool active;

        public void Begin(ArenaSession arenaSession)
        {
            session = arenaSession;
            config = arenaSession.Config;
            nextEnemy = Time.time + startGraceSeconds; // short grace period at run start
            nextOrb = Time.time;
            nextElectron = Time.time + 1f;
            active = true;
        }

        public void Stop()
        {
            active = false;
        }

        /// <summary>Switches to a new level's spawn values (after a mid-run evolution).</summary>
        public void SetConfig(TierConfig tier)
        {
            config = tier;
        }

        private void Update()
        {
            if (!active || session == null || config == null)
            {
                return;
            }

            float minutes = session.Minutes;
            enemies.RemoveAll(e => e == null);
            wildElectrons.RemoveAll(e => e == null || !e.gameObject.activeSelf || e.State != ElectronState.Wild);

            if (Time.time >= nextEnemy)
            {
                nextEnemy = Time.time + config.EnemyIntervalAt(minutes);
                int cap = Mathf.RoundToInt(config.spawn.max_alive_enemies * (1f + aliveCapGrowthPerMinute * minutes));
                if (enemies.Count < cap)
                {
                    SpawnEnemy(minutes);
                }
            }

            if (Time.time >= nextOrb)
            {
                nextOrb = Time.time + config.ScaledIntervalAt(config.spawn.orb_base_interval_s, minutes);
                if (EnergyOrb.Active.Count < maxIdleOrbs && orbPrefab != null) // counts dropped orbs too
                {
                    EnergyOrb.Spawn(orbPrefab, RandomPointInArena(0f)); // pooled, fixed energy
                }
            }

            if (Time.time >= nextElectron && config.electrons != null)
            {
                nextElectron = Time.time + config.ElectronIntervalAt(minutes);
                int wild = electronPool != null ? electronPool.WildCount : wildElectrons.Count;
                if (wild < config.electrons.max_wild)
                {
                    SpawnElectron();
                }
            }
        }

        /// <summary>One wild electron at a free point at least min_spawn_distance from the player.
        /// Skips this tick if no free point is found (never spawns on top of the player).</summary>
        private void SpawnElectron()
        {
            if (!TryRandomPoint(config.electrons.min_spawn_distance, out var point))
            {
                return;
            }

            Electron e = electronPool != null ? electronPool.Get(point) : null;
            if (e == null && electronPrefab != null)
            {
                e = Instantiate(electronPrefab, point, Quaternion.identity);
                wildElectrons.Add(e);
            }

            if (e != null)
            {
                e.MakeWild();
            }
        }

        private void SpawnEnemy(float minutes)
        {
            if (enemyPrefab == null)
            {
                return;
            }

            var enemy = Instantiate(enemyPrefab, RandomPointInArena(minEnemyDistanceFromPlayer), Quaternion.identity);
            enemy.Init(
                session.Player,
                config.EnemyHpAt(minutes),
                config.enemy.move_speed * Mathf.Min(maxEnemySpeedMultiplier, 1f + enemySpeedGrowthPerMinute * minutes),
                config.enemy.sight_range,
                config.enemy.contact_damage,
                config.OrbValueAt(minutes) * 3f);
            enemies.Add(enemy);
        }

        /// <summary>Random free point in the circular arena, at least minDistance from the player.</summary>
        private Vector2 RandomPointInArena(float minDistance)
        {
            if (TryRandomPoint(minDistance, out var free))
            {
                return free;
            }

            // Fallback: opposite side of the arena from the player.
            Vector2 p = session.Player.transform.position;
            return p.sqrMagnitude > 0.01f ? -p.normalized * config.arena.radius * 0.9f : free;
        }

        private bool TryRandomPoint(float minDistance, out Vector2 result)
        {
            float radius = config.arena.radius;
            var layout = session.Layout;
            Vector2 playerPos = session.Player.transform.position;
            result = Vector2.zero;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 point = Random.insideUnitCircle * radius * 0.95f;
                bool blocked = layout != null && layout.IsBlocked(point, 0.6f);
                if (!blocked && (point - playerPos).sqrMagnitude >= minDistance * minDistance)
                {
                    result = point;
                    return true;
                }
            }

            return false;
        }
    }
}
