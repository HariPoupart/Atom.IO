using AtomIO.Arena;
using AtomIO.Core;
using AtomIO.Data;
using AtomIO.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AtomIO.UI
{
    /// <summary>
    /// Top-left HUD: element, level, health bar, energy bar (current / size), time survived and
    /// electrons. Shows an evolve banner when the bar fills mid-run, the win banner at Neon,
    /// and the tutorial hints (GDD 1.4.3): run 1 as H, then the throw / pickup lesson on first
    /// becoming He.
    /// </summary>
    public class ArenaHud : MonoBehaviour
    {
        private static readonly string[] Run1Hints =
        {
            "Drag the left stick to move your atom.",
            "Your orbiting electron hurts enemies. Swing it into them!",
            "Grab glowing energy orbs. Each one fills your energy bar.",
            "Keep enemies off your core: each touch costs 10 HP.",
        };

        private static readonly string[] HeliumHints =
        {
            "Loose electrons drift in the arena. Touch one to fill a slot.",
            "Holding 2 electrons? Drag the right stick to aim the newest.",
            "Let go to throw it. A thrown electron deals 20 damage.",
            "Throw your last electron and you're open to hits until you grab one.",
        };

        [SerializeField] private ArenaSession session;
        [SerializeField] private TMP_Text elementLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private Image energyFill;
        [SerializeField] private TMP_Text energyLabel;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text electronLabel;
        [SerializeField] private TMP_Text tutorialLabel;
        [SerializeField] private TMP_Text bannerLabel;
        [SerializeField] private float hintSeconds = 5f;
        [SerializeField] private float bannerSeconds = 3f;

        private string[] hints;
        private int hintIndex;
        private float nextHintAt;
        private float bannerUntil;

        private void Start()
        {
            if (session == null || session.Config == null)
            {
                enabled = false;
                return;
            }

            session.Player.HealthChanged += OnHealth;
            session.Orbit.ElectronsChanged += OnElectrons;
            session.BarChanged += OnBar;
            session.Evolved += OnEvolved;
            session.Won += OnWon;
            ShowLevel(session.Config);
            OnHealth(session.Player.CurrentHp, session.Player.MaxHp);
            OnElectrons(session.Orbit.Count, session.Orbit.Slots);
            OnBar(MetaProgress.BarEnergy, MetaProgress.BarCapacity);
            ShowBanner($"Level {session.Config.atomic_number}: {session.Config.level_name}");
            StartTutorial();
        }

        private void OnDestroy()
        {
            if (session == null || session.Player == null)
            {
                return;
            }

            session.Player.HealthChanged -= OnHealth;
            session.Orbit.ElectronsChanged -= OnElectrons;
            session.BarChanged -= OnBar;
            session.Evolved -= OnEvolved;
            session.Won -= OnWon;
        }

        private void Update()
        {
            int t = Mathf.FloorToInt(session.TimeSurvived);
            timeLabel.text = $"{t / 60:00}:{t % 60:00}";

            if (bannerLabel != null && bannerLabel.gameObject.activeSelf && Time.time > bannerUntil)
            {
                bannerLabel.gameObject.SetActive(false);
            }

            if (hints != null && tutorialLabel != null && Time.time >= nextHintAt)
            {
                if (hintIndex >= hints.Length)
                {
                    hints = null;
                    tutorialLabel.gameObject.SetActive(false);
                    return;
                }

                tutorialLabel.gameObject.SetActive(true);
                tutorialLabel.text = hints[hintIndex++];
                nextHintAt = Time.time + hintSeconds;
            }
        }

        private void ShowLevel(TierConfig c)
        {
            elementLabel.text = $"{c.symbol}  {c.name}  (Z={c.atomic_number})";
            if (levelLabel != null)
            {
                levelLabel.text = $"Level {c.atomic_number}/{AtomFormulas.MaxAtomicNumber}: {c.level_name}";
            }
        }

        private void OnHealth(int current, int max)
        {
            healthFill.fillAmount = max > 0 ? (float)current / max : 0f;
            healthLabel.text = $"{current}/{max}";
        }

        private void OnElectrons(int count, int slots)
        {
            if (electronLabel != null)
            {
                electronLabel.text = $"e- {count}/{slots}";
            }
        }

        private void OnBar(float energy, int capacity)
        {
            if (energyFill != null)
            {
                energyFill.fillAmount = capacity > 0 ? Mathf.Clamp01(energy / capacity) : 1f;
            }

            energyLabel.text = capacity > 0 ? $"Energy {Mathf.FloorToInt(energy)}/{capacity}" : "Energy MAX";
        }

        private void OnEvolved(TierConfig c)
        {
            ShowLevel(c);
            ShowBanner($"Fusion complete! You are now {c.name}.\nLevel {c.atomic_number}: {c.level_name}");
            if (MetaProgress.TutorialStage == 1)
            {
                StartTutorial();
            }
        }

        private void OnWon()
        {
            ShowBanner("Neon reached! Two full shells: you climbed the whole table.");
        }

        private void ShowBanner(string text)
        {
            if (bannerLabel == null)
            {
                return;
            }

            bannerLabel.text = text;
            bannerLabel.gameObject.SetActive(true);
            bannerUntil = Time.time + bannerSeconds;
        }

        private void StartTutorial()
        {
            hints = null;
            if (MetaProgress.TutorialStage == 0 && MetaProgress.AtomicNumber == 1)
            {
                hints = Run1Hints;
            }
            else if (MetaProgress.TutorialStage == 1)
            {
                hints = HeliumHints;
                MetaProgress.TutorialStage = 2;
            }

            hintIndex = 0;
            nextHintAt = Time.time + (bannerLabel != null && bannerLabel.gameObject.activeSelf ? 1f : 0f);
            if (hints == null && tutorialLabel != null)
            {
                tutorialLabel.gameObject.SetActive(false);
            }
        }
    }
}
