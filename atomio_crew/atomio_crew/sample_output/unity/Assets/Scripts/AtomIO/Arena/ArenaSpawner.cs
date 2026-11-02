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
        [SerializeField] private Electron electronPrefab;
        [SerializeField] private float minEnemyDistanceFromPlayer = 12f;
        [SerializeField] private int maxIdleOrbs = 40;
        [SerializeField] private int maxIdleElectrons = 15;

        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<EnergyOrb> orbs = new List<EnergyOrb>();
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
            nextEnemy = Time.time + 2f; // short grace period at run start
            nextOrb = Time.time;
            nextElectron = Time.time + 1f;
            active = true;
        }

        public void Stop()
        {
            active = false;
        }

        private void Update()
        {
            if (!active || session == null)
            {
                return;
            }

            float minutes = session.Minutes;
            enemies.RemoveAll(e => e == null);
            orbs.RemoveAll(o => o == null);
            wildElectrons.RemoveAll(e => e == null || e.State != ElectronState.Wild);

            if (Time.time >= nextEnemy)
            {
                nextEnemy = Time.time + config.EnemyIntervalAt(minutes);
                if (enemies.Count < config.spawn.max_alive_enemies)
                {
                    SpawnEnemy(minutes);
                }
            }

            if (Time.time >= nextOrb)
            {
                nextOrb = Time.time + config.ScaledIntervalAt(config.spawn.orb_base_interval_s, minutes);
                if (orbs.Count < maxIdleOrbs && orbPrefab != null)
                {
                    var orb = Instantiate(orbPrefab, RandomPointInArena(0f), Quaternion.identity);
                    orb.SetValue(config.OrbValueAt(minutes));
                    orbs.Add(orb);
                }
            }

            if (Time.time >= nextElectron)
            {
                nextElectron = Time.time + config.ScaledIntervalAt(config.spawn.electron_base_interval_s, minutes);
                if (wildElectrons.Count < maxIdleElectrons && electronPrefab != null)
                {
                    var e = Instantiate(electronPrefab, RandomPointInArena(0f), Quaternion.identity);
                    e.MakeWild();
                    wildElectrons.Add(e);
                }
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
                config.enemy.move_speed,
                config.enemy.sight_range,
                config.enemy.contact_damage,
                config.OrbValueAt(minutes) * 3f);
            enemies.Add(enemy);
        }

        /// <summary>Random point in the circular arena, at least minDistance from the player.</summary>
        private Vector2 RandomPointInArena(float minDistance)
        {
            float radius = config.arena.radius;
            Vector2 playerPos = session.Player.transform.position;
            Vector2 point = Vector2.zero;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                point = Random.insideUnitCircle * radius;
                if ((point - playerPos).sqrMagnitude >= minDistance * minDistance)
                {
                    return point;
                }
            }

            // Fallback: opposite side of the arena from the player.
            return playerPos.sqrMagnitude > 0.01f ? -playerPos.normalized * radius * 0.9f : point;
        }
    }
}
