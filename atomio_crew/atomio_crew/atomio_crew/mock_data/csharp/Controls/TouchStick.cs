using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AtomIO.Controls
{
    /// <summary>
    /// Minimal on-screen joystick built on uGUI events (no extra packages).
    /// Put it on a UI Image (the base) with a child RectTransform (the knob).
    /// Left stick = movement, right stick = aim; Released fires on pointer up.
    /// </summary>
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform knob;
        [SerializeField] private float radiusPixels = 120f;
        [SerializeField] private float deadZone = 0.12f;

        public event Action<Vector2> Moved;
        public event Action<Vector2> Released;

        public Vector2 Value { get; private set; }
        public bool Held { get; private set; }

        private RectTransform rect;

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            Vector2 v = Vector2.ClampMagnitude(local / radiusPixels, 1f);
            Value = v.magnitude < deadZone ? Vector2.zero : v;
            if (knob != null)
            {
                knob.anchoredPosition = v * radiusPixels;
            }

            Moved?.Invoke(Value);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Vector2 last = Value;
            Held = false;
            Value = Vector2.zero;
            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }

            Moved?.Invoke(Value);
            Released?.Invoke(last);
        }
    }
}
