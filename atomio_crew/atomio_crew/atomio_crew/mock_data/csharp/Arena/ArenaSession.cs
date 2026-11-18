using System;
using AtomIO.Data;
using AtomIO.Meta;
using AtomIO.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomIO.Arena
{
    /// <summary>
    /// Owns one arena run. Loads the level (tier config) for the player's element, feeds every
    /// bit of collected energy into the energy bar, and when the bar is full evolves the player
    /// on the spot: the next element's stats, look and trait, and the next level's arena. The
    /// bar then empties to 0. Returns to the lobby when the player dies or exits.
    /// Runs before the HUD (execution order) so the HUD always finds a loaded config.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ArenaSession : MonoBehaviour
    {
        public static ArenaSession Instance { get; private set; }

        [SerializeField] private PlayerAtom player;
        [SerializeField] private ElectronOrbit orbit;
        [SerializeField] private ArenaSpawner spawner;
        [SerializeField] private ArenaLayoutBuilder layout;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private float returnDelay = 1.5f;

        /// <summary>Bar energy, bar capacity (0 at Neon).</summary>
        public event Action<float, int> BarChanged;

        /// <summary>Fired after a mid-run evolution, with the new level's config.</summary>
        public event Action<TierConfig> Evolved;

        /// <summary>Fired when the player reaches the final element (Neon).</summary>
        public event Action Won;

        /// <summary>Fired each time energy is collected (amount), before the bar is updated.</summary>
        public event Action<float> EnergyCollected;

        public TierConfig Config { get; private set; }
        public float TimeSurvived { get; private set; }
        public float RunEnergy { get; private set; }
        public float Minutes => TimeSurvived / 60f;
        public bool Running { get; private set; }
        public PlayerAtom Player => player;
        public ElectronOrbit Orbit => orbit;
        public ArenaLayoutBuilder Layout => layout;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (!LoadLevel(MetaProgress.AtomicNumber))
            {
                enabled = false;
                return;
            }

            player.Died += OnPlayerDied;
            spawner.Begin(this);
            Running = true;
        }

        private void Update()
        {
            if (Running)
            {
                TimeSurvived += Time.deltaTime;
            }
        }

        /// <summary>Applies a level: player stats and look, orbit slots, arena theme and obstacles.</summary>
        private bool LoadLevel(int atomicNumber)
        {
            var config = TierConfigLoader.Load(atomicNumber);
            if (config == null)
            {
                return false;
            }

            Config = config;
            player.Init(config); // HP refills to the new maximum
            orbit.Init(config);
            if (arenaCamera != null)
            {
                arenaCamera.backgroundColor = TierConfig.ParseHex(config.arena.background_hex, Color.black);
            }

            if (layout != null)
            {
                layout.Build(config, player.transform.position);
            }

            if (spawner != null)
            {
                spawner.SetConfig(config);
            }

            return true;
        }

        public void AddEnergy(float amount)
        {
            if (!Running || amount <= 0f)
            {
                return;
            }

            EnergyCollected?.Invoke(amount);

            RunEnergy += amount;
            bool evolved = MetaProgress.AddEnergy(amount);
            BarChanged?.Invoke(MetaProgress.BarEnergy, MetaProgress.BarCapacity);
            if (!evolved)
            {
                return;
            }

            // Evolution ends the run (2026-11-17): show the banner, protect the player for the short
            // delay, then return to the lobby, where a popup explains the new element and its power.
            var next = TierConfigLoader.Load(MetaProgress.AtomicNumber);
            if (next != null)
            {
                Evolved?.Invoke(next);
                if (next.evolution.is_final_element)
                {
                    Won?.Invoke();
                }
            }

            if (player != null)
            {
                player.GrantShield(returnDelay + 1f);
            }

            EndRun();
        }

        /// <summary>Hooked to the top-right exit button.</summary>
        public void ExitArena()
        {
            EndRun();
        }

        private void OnPlayerDied()
        {
            EndRun();
        }

        private void EndRun()
        {
            if (!Running)
            {
                return;
            }

            Running = false;
            spawner.Stop();
            MetaProgress.Save(); // the bar keeps its progress between runs
            Invoke(nameof(ReturnToLobby), returnDelay);
        }

        private void ReturnToLobby()
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
