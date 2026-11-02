using System;
using AtomIO.Data;
using AtomIO.Meta;
using AtomIO.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomIO.Arena
{
    /// <summary>
    /// Owns one arena run: loads the tier config for the player's element, tracks time and
    /// energy, and returns to the lobby with the energy banked when the player dies or exits.
    /// </summary>
    public class ArenaSession : MonoBehaviour
    {
        public static ArenaSession Instance { get; private set; }

        [SerializeField] private PlayerAtom player;
        [SerializeField] private ElectronOrbit orbit;
        [SerializeField] private ArenaSpawner spawner;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private float returnDelay = 1.5f;

        public event Action<float> EnergyChanged;

        public TierConfig Config { get; private set; }
        public float TimeSurvived { get; private set; }
        public float Energy { get; private set; }
        public float Minutes => TimeSurvived / 60f;
        public bool Running { get; private set; }
        public PlayerAtom Player => player;
        public ElectronOrbit Orbit => orbit;

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
            Config = TierConfigLoader.Load(MetaProgress.AtomicNumber);
            if (Config == null)
            {
                enabled = false;
                return;
            }

            player.Init(Config);
            orbit.Init(Config);
            player.Died += OnPlayerDied;

            if (arenaCamera != null)
            {
                arenaCamera.backgroundColor = TierConfig.ParseHex(Config.arena.background_hex, Color.black);
            }

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

        public void AddEnergy(float amount)
        {
            if (!Running)
            {
                return;
            }

            Energy += amount;
            EnergyChanged?.Invoke(Energy);
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
            MetaProgress.BankRunEnergy(Mathf.FloorToInt(Energy));
            Invoke(nameof(ReturnToLobby), returnDelay);
        }

        private void ReturnToLobby()
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
