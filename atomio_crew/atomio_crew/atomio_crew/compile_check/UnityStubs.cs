// Minimal compile-only stubs of the Unity APIs Atom.IO scripts are allowed to use.
// Used by the optional `mcs` compile check (validators.compile_check). Not shipped to Unity.
// If the Lead Architect uses an API that is not stubbed here, the check reports it so a
// human can confirm it exists in Unity 6.3 (or add the stub).
#pragma warning disable 0067, 0649, 0414
using System;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => original;
        public static T Instantiate<T>(T original, Vector2 position, Quaternion rotation) where T : Object => original;
        public static void Destroy(Object obj) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static implicit operator bool(Object o) => o != null;
    }
    public class Component : Object
    {
        public Transform transform;
        public GameObject gameObject;
        public T GetComponent<T>() => default(T);
        public T GetComponentInParent<T>() => default(T);
        public T GetComponentInChildren<T>() => default(T);
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour
    {
        public void Invoke(string methodName, float time) { }
        public void CancelInvoke() { }
    }
    public class GameObject : Object { public Transform transform; public void SetActive(bool v) { } public T GetComponent<T>() => default(T); }
    public class Transform : Component { public Vector3 position; public Vector3 localScale; public Quaternion rotation; public Transform parent; }
    public class RectTransform : Transform { public Vector2 anchoredPosition; public Vector2 sizeDelta; }
    public class ScriptableObject : Object { }
    public class TextAsset : Object { public string text; }
    public class Camera : Behaviour { public Color backgroundColor; public static Camera main; public float orthographicSize; }
    public class Renderer : Component { public bool enabled; }
    public class SpriteRenderer : Renderer { public Color color; public Sprite sprite; }
    public class Sprite : Object { }
    public class LineRenderer : Renderer
    {
        public int positionCount;
        public void SetPosition(int index, Vector3 position) { }
    }
    public class TrailRenderer : Renderer { public bool emitting; }
    public class ParticleSystem : Component { public void Play() { } public void Stop() { } }
    public enum RigidbodyType2D { Dynamic, Kinematic, Static }
    public class Rigidbody2D : Component
    {
        public Vector2 linearVelocity;
        public Vector2 position;
        public float gravityScale;
        public bool freezeRotation;
        public RigidbodyType2D bodyType;
        public void MovePosition(Vector2 p) { }
        public void AddForce(Vector2 f) { }
    }
    public class Collider2D : Behaviour { public bool isTrigger; }
    public class CircleCollider2D : Collider2D { public float radius; }
    public class Collision2D { public Collider2D collider; public GameObject gameObject; }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized => this;
        public static Vector2 ClampMagnitude(Vector2 v, float max) => v;
        public static float Distance(Vector2 a, Vector2 b) => 0f;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a) => a;
        public static Vector2 operator *(Vector2 a, float d) => a;
        public static Vector2 operator *(float d, Vector2 a) => a;
        public static Vector2 operator /(Vector2 a, float d) => a;
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float d) => a;
    }
    public struct Quaternion { public static Quaternion identity => new Quaternion(); }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color cyan => new Color(0, 1, 1);
        public static Color clear => new Color(0, 0, 0, 0);
    }
    public static class ColorUtility { public static bool TryParseHtmlString(string s, out Color c) { c = Color.white; return true; } }
    public static class Mathf
    {
        public const float Deg2Rad = 0.0174533f;
        public const float PI = 3.14159265f;
        public static float Max(float a, float b) => a; public static int Max(int a, int b) => a;
        public static float Min(float a, float b) => a; public static int Min(int a, int b) => a;
        public static float Clamp(float v, float a, float b) => v; public static int Clamp(int v, int a, int b) => v;
        public static float Clamp01(float v) => v;
        public static float Pow(float a, float b) => a;
        public static float Sin(float a) => a; public static float Cos(float a) => a;
        public static float Abs(float a) => a; public static float Sqrt(float a) => a;
        public static float Lerp(float a, float b, float t) => a;
        public static int RoundToInt(float f) => 0; public static int FloorToInt(float f) => 0; public static int CeilToInt(float f) => 0;
    }
    public static class Random
    {
        public static float Range(float a, float b) => a; public static int Range(int a, int b) => a;
        public static Vector2 insideUnitCircle => Vector2.zero;
        public static float value => 0f;
    }
    public static class Time { public static float time; public static float deltaTime; public static float fixedDeltaTime; public static float timeScale; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
    public static class JsonUtility { public static T FromJson<T>(string json) => default(T); public static string ToJson(object o) => ""; }
    public static class PlayerPrefs
    {
        public static int GetInt(string k, int d = 0) => d; public static void SetInt(string k, int v) { }
        public static float GetFloat(string k, float d = 0) => d; public static void SetFloat(string k, float v) { }
        public static string GetString(string k, string d = "") => d; public static void SetString(string k, string v) { }
        public static void DeleteKey(string k) { } public static void Save() { } public static bool HasKey(string k) => false;
    }
    public static class Input { public static float GetAxisRaw(string a) => 0f; public static float GetAxis(string a) => 0f; }
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform r, Vector2 p, Camera c, out Vector2 local) { local = Vector2.zero; return true; }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
}

namespace UnityEngine.SceneManagement
{
    public static class SceneManager { public static void LoadScene(string name) { } public static void LoadScene(int index) { } }
}

namespace UnityEngine.EventSystems
{
    public class PointerEventData { public Vector2 position; public Camera pressEventCamera; }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IDragHandler { void OnDrag(PointerEventData e); }
}

namespace UnityEngine.Events
{
    public class UnityEvent { public void AddListener(Action a) { } public void RemoveListener(Action a) { } public void Invoke() { } }
}

namespace UnityEngine.UI
{
    public class Graphic : Behaviour { public Color color; }
    public class Image : Graphic { public float fillAmount; public Sprite sprite; }
    public class Text : Graphic { public string text; }
    public class Button : Behaviour
    {
        public bool interactable;
        public ButtonClickedEvent onClick = new ButtonClickedEvent();
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
    }
    public class Slider : Behaviour { public float value; public float maxValue; }
}

namespace TMPro
{
    public class TMP_Text : UnityEngine.UI.Graphic { public string text; }
    public class TextMeshProUGUI : TMP_Text { }
}
