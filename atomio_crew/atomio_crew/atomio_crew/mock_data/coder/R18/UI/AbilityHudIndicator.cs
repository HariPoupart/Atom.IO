using AtomIO.Abilities;
using AtomIO.Arena;
using AtomIO.Data;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomIO.UI
{
    /// <summary>
    /// Small top-left HUD line under the electron count: the active group ability and whether it is
    /// ready ("Full Shell: ready" / "Full Shell: 12s"). Flashes when the ability fires. Builds itself
    /// at runtime in the arena: no scene wiring.
    /// </summary>
    public class AbilityHudIndicator : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private GroupAbilityController abilities;
        private float flashUntil;
        private float nextRefresh;

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
            var arena = ArenaSession.Instance != null ? ArenaSession.Instance : FindFirstObjectByType<ArenaSession>();
            if (arena != null && arena.GetComponent<AbilityHudIndicator>() == null)
            {
                arena.gameObject.AddComponent<AbilityHudIndicator>();
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

            var go = new GameObject("AbilityIndicator", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(40f, -316f); // under the "e- 1/1" line of the HUD
            rt.sizeDelta = new Vector2(760f, 40f);

            label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.text = string.Empty;
        }

        private void Update()
        {
            if (label == null)
            {
                return;
            }

            if (abilities == null)
            {
                abilities = GroupAbilityController.Instance;
                if (abilities == null)
                {
                    return;
                }

                abilities.Triggered += OnTriggered;
            }

            if (Time.time < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.time + 0.1f;
            GroupAbility a = abilities.Current;
            if (a == null)
            {
                label.text = string.Empty;
                return;
            }

            string state = a.trigger == "passive" ? "always on"
                : abilities.IsReady ? "ready" : $"{Mathf.CeilToInt(abilities.CooldownRemaining)}s";
            bool flashing = Time.time < flashUntil;
            label.color = flashing ? new Color(1f, 0.95f, 0.4f) : new Color(0.7f, 0.95f, 1f);
            label.text = $"Ability: <b>{a.name}</b>: {state}";
        }

        private void OnTriggered(GroupAbility ability)
        {
            flashUntil = Time.time + 0.6f;
            nextRefresh = 0f;
        }

        private void OnDestroy()
        {
            if (abilities != null)
            {
                abilities.Triggered -= OnTriggered;
            }
        }
    }
}
