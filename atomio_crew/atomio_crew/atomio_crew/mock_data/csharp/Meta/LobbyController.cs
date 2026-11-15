using AtomIO.Core;
using AtomIO.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomIO.Meta
{
    /// <summary>
    /// Lobby: shows the current element, its level and the energy bar, and starts a run.
    /// There is no evolve button: evolving happens in the arena the moment the bar is full.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private string arenaSceneName = "Arena";
        [SerializeField] private TMP_Text elementLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Image energyFill;
        [SerializeField] private TMP_Text energyLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Image corePreview;

        private void Start()
        {
            startButton.onClick.AddListener(OnStart);
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnReset);
            }

            Refresh();
        }

        private void Refresh()
        {
            int z = MetaProgress.AtomicNumber;
            var config = TierConfigLoader.Load(z);
            string title = config != null ? $"{config.symbol}  {config.name}" : $"Z={z}";
            elementLabel.text = $"{title}  (Z={z})";
            if (levelLabel != null)
            {
                levelLabel.text = config != null
                    ? $"Level {z}/{AtomFormulas.MaxAtomicNumber}: {config.level_name}"
                    : $"Level {z}/{AtomFormulas.MaxAtomicNumber}";
            }

            if (corePreview != null && config != null)
            {
                corePreview.color = TierConfig.ParseHex(config.core_visual.primary_hex, Color.white);
            }

            if (energyFill != null)
            {
                energyFill.fillAmount = MetaProgress.BarFill01;
            }

            if (MetaProgress.HasWon)
            {
                energyLabel.text = "Energy: MAX";
                infoLabel.text = "Neon reached! Two full shells. Play Neon's arena as long as you like.";
                return;
            }

            energyLabel.text = $"Energy {Mathf.FloorToInt(MetaProgress.BarEnergy)}/{MetaProgress.BarCapacity}";
            var next = TierConfigLoader.Load(z + 1);
            string nextName = next != null ? next.name : $"Z={z + 1}";
            infoLabel.text = MetaProgress.TutorialStage == 0
                ? "Collect energy orbs to fill the bar. A full bar evolves you on the spot!"
                : $"Fill the bar to evolve into {nextName}.";
        }

        private void OnStart()
        {
            SceneManager.LoadScene(arenaSceneName);
        }

        private void OnReset()
        {
            MetaProgress.ResetAll();
            Refresh();
        }
    }
}
