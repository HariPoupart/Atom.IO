using AtomIO.Arena;
using AtomIO.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AtomIO.UI
{
    /// <summary>
    /// Top-left HUD: element, health bar, energy collected, time survived, electrons.
    /// Also shows the tutorial hint for the first two runs (GDD 1.4.3).
    /// </summary>
    public class ArenaHud : MonoBehaviour
    {
        [SerializeField] private ArenaSession session;
        [SerializeField] private TMP_Text elementLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text energyLabel;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text electronLabel;
        [SerializeField] private TMP_Text tutorialLabel;

        private void Start()
        {
            if (session == null || session.Config == null)
            {
                enabled = false;
                return;
            }

            var c = session.Config;
            elementLabel.text = $"{c.symbol}  {c.name}  (Z={c.atomic_number})";
            session.Player.HealthChanged += OnHealth;
            session.Orbit.ElectronsChanged += OnElectrons;
            session.EnergyChanged += OnEnergy;
            OnHealth(session.Player.CurrentHp, session.Player.MaxHp);
            OnElectrons(session.Orbit.Count, session.Orbit.Slots);
            OnEnergy(0f);
            ShowTutorial();
        }

        private void OnDestroy()
        {
            if (session == null || session.Player == null)
            {
                return;
            }

            session.Player.HealthChanged -= OnHealth;
            session.Orbit.ElectronsChanged -= OnElectrons;
            session.EnergyChanged -= OnEnergy;
        }

        private void Update()
        {
            int t = Mathf.FloorToInt(session.TimeSurvived);
            timeLabel.text = $"{t / 60:00}:{t % 60:00}";
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

        private void OnEnergy(float energy)
        {
            energyLabel.text = $"Energy {Mathf.FloorToInt(energy)}";
        }

        private void ShowTutorial()
        {
            if (tutorialLabel == null)
            {
                return;
            }

            switch (MetaProgress.TutorialStage)
            {
                case 0:
                    tutorialLabel.text = "Move into enemies: your orbiting electron damages them!";
                    break;
                case 2:
                    tutorialLabel.text = "Drag the right stick and let go to throw a spare electron. Touch loose electrons to collect them.";
                    MetaProgress.TutorialStage = 3;
                    break;
                default:
                    tutorialLabel.gameObject.SetActive(false);
                    break;
            }
        }
    }
}
