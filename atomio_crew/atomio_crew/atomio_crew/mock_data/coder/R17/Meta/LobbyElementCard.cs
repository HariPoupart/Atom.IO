using AtomIO.Core;
using AtomIO.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomIO.Meta
{
    /// <summary>
    /// Shows the current element's card (title + flavor from element_cards.json) and the next element
    /// at the bottom of the lobby, so the next evolution is always in sight (GDD 1.4.4). Attaches
    /// itself to the LobbyController at runtime and builds its label in code: no scene wiring.
    /// </summary>
    public class LobbyElementCard : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private int shownElement = -1;

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
            var lobby = FindFirstObjectByType<LobbyController>();
            if (lobby != null && lobby.GetComponent<LobbyElementCard>() == null)
            {
                lobby.gameObject.AddComponent<LobbyElementCard>();
            }
        }

        private void Start()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                enabled = false;
                return;
            }

            var go = new GameObject("ElementCard", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);   // below PLAY, clear of the Reset button
            rt.sizeDelta = new Vector2(1200f, 130f);

            label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(0.85f, 0.95f, 1f, 0.95f);
            label.raycastTarget = false;
            Refresh();
        }

        private void Update()
        {
            // The Reset button can send the player back to Hydrogen: follow the saved element.
            if (label != null && MetaProgress.AtomicNumber != shownElement)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            int z = MetaProgress.AtomicNumber;
            shownElement = z;
            var card = ElementCardDatabase.ForElement(z);
            string head = card != null ? $"<b>{card.symbol} - {card.title}</b>\n<i>{card.flavor}</i>" : $"Element {z}";

            string next;
            if (z >= AtomFormulas.FinalAtomicNumber)
            {
                next = "Periodic table complete: you reached Neon!";
            }
            else
            {
                var nextCard = ElementCardDatabase.ForElement(z + 1);
                var nextLevel = TierConfigLoader.Load(z + 1);
                string nextName = nextLevel != null ? nextLevel.name : (nextCard != null ? nextCard.symbol : $"Z={z + 1}");
                next = $"Next evolution: {nextName}" + (nextCard != null ? $" ({nextCard.title})" : "");
            }

            label.text = $"{head}\n<size=80%>{next}</size>";
        }
    }
}
