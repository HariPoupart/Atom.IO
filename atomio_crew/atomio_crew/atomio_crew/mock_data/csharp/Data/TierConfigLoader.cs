using System.Collections.Generic;
using AtomIO.Core;
using UnityEngine;

namespace AtomIO.Data
{
    /// <summary>
    /// Loads pre-generated tier configs from Resources/AtomIO/Tiers.
    /// No network access: everything ships inside the build (GDD 3.1.2).
    /// </summary>
    public static class TierConfigLoader
    {
        private const string Folder = "AtomIO/Tiers/";
        private const string FallbackFile = "tier_fallback";
        private static readonly Dictionary<int, TierConfig> Cache = new Dictionary<int, TierConfig>();

        public static TierConfig Load(int atomicNumber)
        {
            atomicNumber = UnityEngine.Mathf.Clamp(atomicNumber, 1, AtomFormulas.MaxAtomicNumber);
            if (Cache.TryGetValue(atomicNumber, out var cached))
            {
                return cached;
            }

            var config = TryLoad($"{Folder}tier_{atomicNumber:000}");
            if (config == null)
            {
                Debug.LogWarning($"[Atom.IO] Tier {atomicNumber} missing, using fallback config.");
                config = TryLoad(Folder + FallbackFile);
                if (config != null)
                {
                    // Fallback keeps its look but must still obey the GDD numbers for this element.
                    config.atomic_number = atomicNumber;
                    config.player.max_hp = AtomFormulas.MaxHp(atomicNumber);
                    config.player.electron_slots = AtomFormulas.ElectronSlots(atomicNumber);
                    bool last = AtomFormulas.IsFinalElement(atomicNumber);
                    config.evolution.next_atomic_number = last ? 0 : atomicNumber + 1;
                    config.evolution.energy_to_evolve = AtomFormulas.EnergyToEvolve(atomicNumber);
                    config.evolution.bar_reset_value = AtomFormulas.BarResetValue;
                    config.evolution.is_final_element = last;
                    if (config.electrons != null)
                    {
                        config.electrons.max_wild = atomicNumber == 1 ? 0 : Mathf.Max(config.electrons.max_wild, atomicNumber);
                    }
                }
            }

            if (config == null)
            {
                Debug.LogError("[Atom.IO] Fallback tier config missing. Did you promote the crew output?");
                return null;
            }

            Cache[atomicNumber] = config;
            return config;
        }

        private static TierConfig TryLoad(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                return null;
            }

            var config = JsonUtility.FromJson<TierConfig>(asset.text);
            return config != null && config.player != null ? config : null;
        }
    }
}
