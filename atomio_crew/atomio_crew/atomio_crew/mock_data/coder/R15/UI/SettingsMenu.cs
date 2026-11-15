using AtomIO.Arena;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomIO.UI
{
    /// <summary>
    /// Top-right settings button in the arena (GDD 1.4.2), next to EXIT. Opening it pauses the game
    /// (timeScale 0) and shows Resume, Sound on/off (saved in PlayerPrefs) and Exit to lobby.
    /// Builds its UI in code at runtime: no scene wiring.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private const string SoundKey = "atomio.sound_on";

        private GameObject panel;
        private TextMeshProUGUI soundLabel;
        public bool IsOpen => panel != null && panel.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplySound();
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            var arena = ArenaSession.Instance != null ? ArenaSession.Instance : FindFirstObjectByType<ArenaSession>();
            if (arena != null && arena.GetComponent<SettingsMenu>() == null)
            {
                arena.gameObject.AddComponent<SettingsMenu>();
            }
        }

        private static bool SoundOn => PlayerPrefs.GetInt(SoundKey, 1) == 1;

        private static void ApplySound() => AudioListener.pause = !SoundOn;

        private void Start()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                enabled = false;
                return;
            }

            // Button left of EXIT (EXIT sits at the top-right corner, 200 x 80).
            MakeButton(canvas.transform, "SettingsButton", "MENU", new Vector2(1f, 1f), new Vector2(-260f, -30f),
                       new Vector2(200f, 80f), Open);

            // Full-screen dimmer that also blocks touches on the sticks while paused.
            panel = new GameObject("SettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var center = new Vector2(0.5f, 0.5f);
            var title = MakeLabel(panel.transform, "Paused", 64f, center, new Vector2(0f, 220f), new Vector2(800f, 90f));
            title.fontStyle = FontStyles.Bold;
            MakeButton(panel.transform, "Resume", "Resume", center, new Vector2(0f, 80f), new Vector2(420f, 100f), Resume);
            soundLabel = MakeButton(panel.transform, "Sound", "", center, new Vector2(0f, -40f), new Vector2(420f, 100f),
                                    ToggleSound);
            MakeButton(panel.transform, "ExitToLobby", "Exit to lobby", center, new Vector2(0f, -160f),
                       new Vector2(420f, 100f), ExitToLobby);
            RefreshSoundLabel();
            panel.SetActive(false);
        }

        public void Open()
        {
            if (panel == null)
            {
                return;
            }

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            Time.timeScale = 1f;
        }

        private void ToggleSound()
        {
            PlayerPrefs.SetInt(SoundKey, SoundOn ? 0 : 1);
            PlayerPrefs.Save();
            ApplySound();
            RefreshSoundLabel();
        }

        private void ExitToLobby()
        {
            Resume(); // the return-to-lobby delay runs on scaled time
            var arena = ArenaSession.Instance;
            if (arena != null)
            {
                arena.ExitArena();
            }
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel != null)
            {
                soundLabel.text = SoundOn ? "Sound: On" : "Sound: Off";
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f; // never leave the next scene paused
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Vector2 anchor,
                                                 Vector2 pos, Vector2 box)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static TextMeshProUGUI MakeButton(Transform parent, string name, string text, Vector2 anchor,
                                                  Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.12f, 0.2f, 0.32f, 0.9f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var label = MakeLabel(go.transform, text, 36f, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return label;
        }
    }
}
