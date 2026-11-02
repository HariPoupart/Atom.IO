using AtomIO.Core;
using AtomIO.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomIO.Meta
{
    /// <summary>
    /// Lobby: shows the current element and energy, evolves (with failure chance) or starts a run.
    /// After the first death the tutorial forces the H to He evolution before the next run.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private string arenaSceneName = "Arena";
        [SerializeField] private TMP_Text elementLabel;
        [SerializeField] private TMP_Text energyLabel;
        [SerializeField] private TMP_Text evolveInfoLabel;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private Button evolveButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Image corePreview;

        private void Start()
        {
            evolveButton.onClick.AddListener(OnEvolve);
            startButton.onClick.AddListener(OnStart);
            resultLabel.text = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            int z = MetaProgress.AtomicNumber;
            var config = TierConfigLoader.Load(z);
            string title = config != null ? $"{config.symbol}  {config.name}" : $"Z={z}";
            elementLabel.text = $"{title}  (Z={z})";
            energyLabel.text = $"Energy: {MetaProgress.Energy}";

            if (corePreview != null && config != null)
            {
                corePreview.color = TierConfig.ParseHex(config.core_visual.primary_hex, Color.white);
            }

            if (MetaProgress.HasWon)
            {
                evolveInfoLabel.text = "You reached Oganesson. The table is complete!";
                evolveButton.interactable = false;
                startButton.interactable = true;
                return;
            }

            int cost = MetaProgress.NextCost;
            float fail = MetaProgress.CurrentFailChance;
            evolveInfoLabel.text = $"Evolve to Z={z + 1}: {cost} energy, {fail:0.0}% failure chance";
            evolveButton.interactable = MetaProgress.Energy >= cost;

            // Tutorial: after the first death the player must evolve before playing again.
            bool mustEvolve = MetaProgress.TutorialStage == 1;
            startButton.interactable = !mustEvolve;
            if (mustEvolve)
            {
                resultLabel.text = "Spend your energy to evolve into Helium!";
            }
        }

        private void OnEvolve()
        {
            switch (MetaProgress.TryEvolve())
            {
                case EvolveResult.Success:
                    resultLabel.text = "Evolution successful!";
                    break;
                case EvolveResult.Failed:
                    resultLabel.text = $"Evolution failed. Energy lost. Next try: -{AtomFormulas.PityStepPercent}% failure chance.";
                    break;
                case EvolveResult.NotEnoughEnergy:
                    resultLabel.text = "Not enough energy. Go collect more in the arena.";
                    break;
                case EvolveResult.AlreadyMaxElement:
                    resultLabel.text = "Already at Oganesson.";
                    break;
            }

            Refresh();
        }

        private void OnStart()
        {
            SceneManager.LoadScene(arenaSceneName);
        }
    }
}
