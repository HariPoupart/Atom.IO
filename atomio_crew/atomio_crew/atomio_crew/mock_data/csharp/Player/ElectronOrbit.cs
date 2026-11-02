using System;
using System.Collections.Generic;
using AtomIO.Combat;
using AtomIO.Data;
using UnityEngine;

namespace AtomIO.Player
{
    /// <summary>
    /// Bohr-model electron shells around the player. Slots = atomic number.
    /// Always keeps at least one electron; only fires when holding more than one.
    /// Shells fill 2, 8, 18, 32... like the Bohr model.
    /// </summary>
    [RequireComponent(typeof(PlayerAtom))]
    public class ElectronOrbit : MonoBehaviour
    {
        [SerializeField] private Electron electronPrefab;
        [SerializeField] private float firstShellRadius = 1.2f;
        [SerializeField] private float shellSpacing = 0.6f;
        [SerializeField] private float baseDegreesPerSecond = 120f;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private float aimLineLength = 3f;

        public event Action<int, int> ElectronsChanged; // count, slots

        public int Count => electrons.Count;
        public int Slots { get; private set; } = 1;
        public bool CanRelease => electrons.Count > minOrbiting;

        private readonly List<Electron> electrons = new List<Electron>();
        private int minOrbiting = 1;
        private int orbitDamage = 10;
        private int releaseDamage = 20;
        private float damageMult = 1f;
        private float orbitSpeedMult = 1f;
        private float angle;

        public void Init(TierConfig config)
        {
            Slots = config.player.electron_slots;
            minOrbiting = Mathf.Max(1, config.player.min_orbiting_electrons);
            orbitDamage = config.player.orbiting_electron_damage;
            releaseDamage = config.player.released_electron_damage;
            damageMult = config.trait != null ? config.trait.electron_damage_mult : 1f;
            orbitSpeedMult = config.trait != null ? config.trait.orbit_speed_mult : 1f;

            // Every run starts with the guaranteed electron(s).
            while (electrons.Count < minOrbiting && electronPrefab != null)
            {
                var e = Instantiate(electronPrefab, transform.position, Quaternion.identity);
                Add(e);
            }

            ShowAim(false, Vector2.zero);
        }

        public bool TryCollect(Electron electron)
        {
            if (electrons.Count >= Slots || electrons.Contains(electron))
            {
                return false;
            }

            Add(electron);
            return true;
        }

        private void Add(Electron electron)
        {
            electron.AttachTo(this, orbitDamage, releaseDamage, damageMult);
            electrons.Add(electron);
            ElectronsChanged?.Invoke(electrons.Count, Slots);
        }

        /// <summary>Aim stick held: draw the drag vector on the newest electron.</summary>
        public void Aim(Vector2 direction)
        {
            ShowAim(CanRelease && direction.sqrMagnitude > 0.01f, direction);
        }

        /// <summary>Aim stick released: fire the newest electron.</summary>
        public bool Release(Vector2 direction)
        {
            ShowAim(false, Vector2.zero);
            if (!CanRelease || direction.sqrMagnitude < 0.01f)
            {
                return false;
            }

            int last = electrons.Count - 1;
            var e = electrons[last];
            electrons.RemoveAt(last);
            e.Release(direction);
            ElectronsChanged?.Invoke(electrons.Count, Slots);
            return true;
        }

        private void FixedUpdate()
        {
            angle += baseDegreesPerSecond * orbitSpeedMult * Time.fixedDeltaTime;
            Vector2 center = transform.position;

            int index = 0;
            int shell = 0;
            while (index < electrons.Count)
            {
                int capacity = 2 * (shell + 1) * (shell + 1); // 2, 8, 18, 32...
                int inShell = Mathf.Min(capacity, electrons.Count - index);
                float radius = firstShellRadius + shell * shellSpacing;
                float direction = shell % 2 == 0 ? 1f : -1f; // alternate spin per shell
                for (int i = 0; i < inShell; i++)
                {
                    float a = (angle * direction + 360f * i / inShell) * Mathf.Deg2Rad;
                    var e = electrons[index + i];
                    if (e == null)
                    {
                        continue;
                    }

                    e.SetOrbitPosition(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }

                index += inShell;
                shell++;
            }

            electrons.RemoveAll(e => e == null);
        }

        private void ShowAim(bool visible, Vector2 direction)
        {
            if (aimLine == null)
            {
                return;
            }

            aimLine.enabled = visible;
            if (!visible || electrons.Count == 0)
            {
                return;
            }

            Vector3 start = electrons[electrons.Count - 1].transform.position;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, start + (Vector3)(direction.normalized * aimLineLength));
        }
    }
}
