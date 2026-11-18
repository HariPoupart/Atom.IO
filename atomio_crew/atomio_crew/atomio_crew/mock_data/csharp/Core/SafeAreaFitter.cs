using System.Collections.Generic;
using UnityEngine;

namespace AtomIO.Core
{
    /// <summary>
    /// Moves a canvas's corner- and edge-anchored children inside Screen.safeArea. Full-screen
    /// children (backgrounds, dimmers) still cover the whole screen. Children added later by
    /// code are picked up automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        private struct Original
        {
            public Vector2 offsetMin;
            public Vector2 offsetMax;
        }

        private readonly Dictionary<RectTransform, Original> originals = new Dictionary<RectTransform, Original>();
        private Canvas canvas;
        private Rect appliedArea;
        private float appliedScale = -1f;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        private void OnEnable()
        {
            appliedScale = -1f;
        }

        private void OnTransformChildrenChanged()
        {
            appliedScale = -1f; // re-apply next frame, including the new children
        }

        private void LateUpdate()
        {
            if (canvas == null)
            {
                return;
            }

            float scale = canvas.scaleFactor;
            Rect area = Screen.safeArea;
            if (Mathf.Approximately(scale, appliedScale) && area == appliedArea)
            {
                return;
            }

            appliedScale = scale;
            appliedArea = area;
            Apply(area, scale);
        }

        private void Apply(Rect area, float scale)
        {
            if (scale <= 0f)
            {
                return;
            }

            float left = area.xMin / scale;
            float right = (Screen.width - area.xMax) / scale;
            float bottom = area.yMin / scale;
            float top = (Screen.height - area.yMax) / scale;

            for (int i = 0; i < transform.childCount; i++)
            {
                if (!(transform.GetChild(i) is RectTransform rt))
                {
                    continue;
                }

                bool stretchX = rt.anchorMin.x <= 0f && rt.anchorMax.x >= 1f;
                bool stretchY = rt.anchorMin.y <= 0f && rt.anchorMax.y >= 1f;
                if (stretchX && stretchY)
                {
                    continue; // full-screen layer: leave it covering the whole screen
                }

                if (!originals.TryGetValue(rt, out var o))
                {
                    o = new Original { offsetMin = rt.offsetMin, offsetMax = rt.offsetMax };
                    originals[rt] = o;
                }

                Vector2 min = o.offsetMin;
                Vector2 max = o.offsetMax;
                if (stretchX) { min.x += left; max.x -= right; }
                else if (rt.anchorMax.x <= 0f) { min.x += left; max.x += left; }
                else if (rt.anchorMin.x >= 1f) { min.x -= right; max.x -= right; }

                if (stretchY) { min.y += bottom; max.y -= top; }
                else if (rt.anchorMax.y <= 0f) { min.y += bottom; max.y += bottom; }
                else if (rt.anchorMin.y >= 1f) { min.y -= top; max.y -= top; }

                rt.offsetMin = min;
                rt.offsetMax = max;
            }
        }
    }
}
