using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomIO.Core
{
    /// <summary>
    /// Makes the game fit any phone or tablet screen, with no scene wiring:
    ///  - landscape only (either way up, so the phone can be flipped),
    ///  - UI scales from the screen height on wide phones (20:9 etc.), so the extra width goes to
    ///    the sides instead of shrinking or stretching the HUD,
    ///  - corner UI (HUD, sticks, buttons) is pushed inside the safe area, away from notches,
    ///    camera holes and rounded corners,
    ///  - on screens narrower than 16:9 (tablets) the camera zooms out so the player still sees
    ///    the same width of arena.
    /// </summary>
    public class ScreenFit : MonoBehaviour
    {
        public const float DesignAspect = 16f / 9f;
        public const float DesignOrthoSize = 9f;

        private static ScreenFit instance;
        private Vector2Int lastSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.AutoRotation; // limited to the two landscapes above
            }

            var go = new GameObject("ScreenFit");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFit>();
            SceneManager.sceneLoaded += (_, __) => instance.StartCoroutine(instance.FitSoon());
        }

        // Features build their canvases on sceneLoaded/Start, so fit now and again over the next frames.
        private IEnumerator FitSoon()
        {
            for (int i = 0; i < 3; i++)
            {
                FitAll();
                yield return null;
            }
        }

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != lastSize) // rotation or window resize
            {
                FitAll();
            }
        }

        private void FitAll()
        {
            lastSize = new Vector2Int(Screen.width, Screen.height);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : DesignAspect;

            foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    continue;
                }

                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = aspect >= DesignAspect ? 1f : 0f; // wide phone: height; tablet: width
                var canvas = scaler.GetComponent<Canvas>();
                if (canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                    !scaler.TryGetComponent<SafeAreaFitter>(out _))
                {
                    scaler.gameObject.AddComponent<SafeAreaFitter>();
                }
            }

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                // Keep at least the 16:9 view width on narrower screens; wider phones simply see more.
                cam.orthographicSize = aspect < DesignAspect ? DesignOrthoSize * DesignAspect / aspect : DesignOrthoSize;
            }
        }
    }
}
