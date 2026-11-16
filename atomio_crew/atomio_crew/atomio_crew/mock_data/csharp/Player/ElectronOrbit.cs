using System;
using System.Collections.Generic;
using AtomIO.Arena;
using AtomIO.Combat;
using AtomIO.Data;
using UnityEngine;

namespace AtomIO.Player
{
    /// <summary>
    /// Bohr-model electron shells around the player. Slots = atomic number.
    /// Always keeps at least one electron; only fires when holding more than one.
    /// Shells fill 2, then 8, like the Bohr model (Neon fills both).
    /// </summary>
    /// <summary>
    /// Bohr-model electron shells around the player. Slots = atomic number.
    /// Always keeps at least one electron; only fires when holding more than one.
    /// Shells fill 2, then 8, like the Bohr model (Neon fills both).
    ///
    /// Motion (after the developer's demo ElectronMovement script): positions are set directly
    /// every rendered frame in LateUpdate, after the player has moved, so electrons never
    /// stutter against the interpolated player and camera. Each electron keeps its own angle
    /// offset and radius and glides to its new slot when the count changes, instead of
    /// teleporting. Shell radii grow with the core's size.
    /// </summary>
    [RequireComponent(typeof(PlayerAtom))]
    public class ElectronOrbit : MonoBehaviour
    {
        private class Orbiter
        {
            public Electron electron;
            public float offset;  // degrees, relative to the shell's spinning base angle
            public float radius;  // current distance from the nucleus
        }

        [SerializeField] private Electron electronPrefab;
        [SerializeField] private float firstShellGap = 0.7f;   // from the core's edge to shell 1
        [SerializeField] private float shellSpacing = 0.6f;
        [SerializeField] private float baseDegreesPerSecond = 120f;
        [SerializeField] private float settleSpeed = 6f;       // how fast electrons glide into their slot
        [Header("Valence tuning (2026-11-15)")]
        // Renamed from spinMultiplier (was 1.75, too fast to aim) so an old saved value can't override it.
        [SerializeField] private float orbitSpinMultiplier = 1.15f; // inner shell: 120 -> ~138 deg/s
        [Tooltip("1 = every shell moves at the same speed along its circle, so outer shells turn more slowly.")]
        [SerializeField, Range(0f, 1f)] private float outerShellSlowdown = 1f;
        [SerializeField] private float extraShellGap = 0.8f;    // shell 1 sits 0.8 units further from the core
        [SerializeField] private float extraShellSpacing = 0.25f; // and shells are spread a little wider
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private float aimLineLength = 3f;

        public event Action<int, int> ElectronsChanged; // count, slots

        public int Count => orbiters.Count;
        public int Slots { get; private set; } = 1;
        public bool CanRelease => orbiters.Count > minOrbiting;

        private readonly List<Orbiter> orbiters = new List<Orbiter>();
        private CircleCollider2D core;
        private int minOrbiting = 1;
        private int orbitDamage = 10;
        private int releaseDamage = 20;
        private float damageMult = 1f;
        private float orbitSpeedMult = 1f;
        private readonly float[] shellAngles = new float[4]; // each shell spins on its own angle
        private Vector2 aimDirection;

        private void Awake()
        {
            core = GetComponent<CircleCollider2D>();
        }

        public void Init(TierConfig config)
        {
            Slots = config.player.electron_slots;
            // Hydrogen (min 0) may throw its only electron; every other element keeps at least 1.
            minOrbiting = Mathf.Max(0, config.player.min_orbiting_electrons);
            orbitDamage = config.player.orbiting_electron_damage;
            releaseDamage = config.player.released_electron_damage;
            damageMult = config.trait != null ? config.trait.electron_damage_mult : 1f;
            orbitSpeedMult = config.trait != null ? config.trait.orbit_speed_mult : 1f;

            // Every run starts with the guaranteed electron(s).
            var pool = ElectronPool.Instance;
            while (orbiters.Count < Mathf.Max(1, minOrbiting)) // every run / level starts with an electron
            {
                Electron e = pool != null ? pool.Get(transform.position) : null;
                if (e == null && electronPrefab != null)
                {
                    e = Instantiate(electronPrefab, transform.position, Quaternion.identity);
                }

                if (e == null)
                {
                    Debug.LogError("[Atom.IO] No Electron prefab on the pool or the orbit: the player has no electron.");
                    break;
                }

                Add(e);
            }

            ShowAim(false, Vector2.zero);
        }

        public bool TryCollect(Electron electron)
        {
            if (orbiters.Count >= Slots || IndexOf(electron) >= 0)
            {
                return false;
            }

            Add(electron);
            electron.PlayPickupFeedback();
            return true;
        }

        private void Add(Electron electron)
        {
            electron.AttachTo(this, orbitDamage, releaseDamage, damageMult);

            // Start from where the electron actually is, then glide into the slot.
            int index = orbiters.Count;
            Vector2 rel = (Vector2)electron.transform.position - (Vector2)transform.position;
            float startAngle = rel.sqrMagnitude > 0.0001f ? Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg : 0f;
            orbiters.Add(new Orbiter
            {
                electron = electron,
                offset = startAngle - ShellAngle(ShellOf(index)) * ShellDirection(ShellOf(index)),
                radius = Mathf.Max(CoreRadius(), rel.magnitude),
            });
            ElectronsChanged?.Invoke(orbiters.Count, Slots);
        }

        /// <summary>Aim stick held: draw the drag vector on the newest electron.</summary>
        public void Aim(Vector2 direction)
        {
            aimDirection = direction;
            ShowAim(CanRelease && direction.sqrMagnitude > 0.01f, direction);
        }

        /// <summary>Aim stick released: fire the newest electron.</summary>
        public bool Release(Vector2 direction)
        {
            aimDirection = Vector2.zero;
            ShowAim(false, Vector2.zero);
            if (!CanRelease || direction.sqrMagnitude < 0.01f)
            {
                return false;
            }

            int last = orbiters.Count - 1;
            var e = orbiters[last].electron;
            orbiters.RemoveAt(last);
            e.Release(direction);
            ElectronsChanged?.Invoke(orbiters.Count, Slots);
            return true;
        }

        private void LateUpdate()
        {
            orbiters.RemoveAll(o => o.electron == null || o.electron.State != ElectronState.Orbiting);

            float innerDegPerSec = baseDegreesPerSecond * orbitSpinMultiplier * orbitSpeedMult;
            float blend = 1f - Mathf.Exp(-settleSpeed * Time.deltaTime);
            Vector2 center = transform.position;
            float coreRadius = CoreRadius();

            int index = 0;
            int shell = 0;
            while (index < orbiters.Count)
            {
                int capacity = 2 * (shell + 1) * (shell + 1); // 2, 8, ...
                int inShell = Mathf.Min(capacity, orbiters.Count - index);
                float shellRadius = coreRadius + firstShellGap + extraShellGap + shell * (shellSpacing + extraShellSpacing);
                float dir = ShellDirection(shell);
                // Outer shells turn more slowly so their electrons don't whip around much faster than
                // the inner shell's (same speed along the circle when outerShellSlowdown = 1).
                float innerRadius = coreRadius + firstShellGap + extraShellGap;
                float slow = Mathf.Lerp(1f, innerRadius / Mathf.Max(0.01f, shellRadius), outerShellSlowdown);
                int si = Mathf.Min(shell, shellAngles.Length - 1);
                shellAngles[si] = Mathf.Repeat(shellAngles[si] + innerDegPerSec * slow * Time.deltaTime, 360f);
                for (int i = 0; i < inShell; i++)
                {
                    var o = orbiters[index + i];
                    o.offset = Mathf.LerpAngle(o.offset, 360f * i / inShell, blend);
                    o.radius = Mathf.Lerp(o.radius, shellRadius, blend);
                    float a = (shellAngles[si] * dir + o.offset) * Mathf.Deg2Rad;
                    o.electron.SetOrbitPosition(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * o.radius);
                }

                index += inShell;
                shell++;
            }

            if (aimLine != null && aimLine.enabled)
            {
                ShowAim(true, aimDirection);
            }
        }

        private float CoreRadius()
        {
            float r = core != null ? core.radius : 0.5f;
            return r * Mathf.Abs(transform.localScale.x);
        }

        private static int ShellOf(int index)
        {
            int shell = 0;
            int capacity = 2;
            while (index >= capacity)
            {
                index -= capacity;
                shell++;
                capacity = 2 * (shell + 1) * (shell + 1);
            }

            return shell;
        }

        private float ShellAngle(int shell) => shellAngles[Mathf.Min(shell, shellAngles.Length - 1)];

        private static float ShellDirection(int shell) => shell % 2 == 0 ? 1f : -1f; // alternate spin per shell

        private int IndexOf(Electron electron)
        {
            for (int i = 0; i < orbiters.Count; i++)
            {
                if (orbiters[i].electron == electron)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ShowAim(bool visible, Vector2 direction)
        {
            if (aimLine == null)
            {
                return;
            }

            aimLine.enabled = visible;
            if (!visible || orbiters.Count == 0)
            {
                return;
            }

            Vector3 start = orbiters[orbiters.Count - 1].electron.transform.position;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, start + (Vector3)(direction.normalized * aimLineLength));
        }
    }
}
