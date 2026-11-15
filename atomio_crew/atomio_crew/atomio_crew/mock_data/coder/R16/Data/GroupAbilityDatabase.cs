using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtomIO.Data
{
    /// <summary>One element-group ability, as written by the content pipeline (group_abilities.json).</summary>
    [Serializable]
    public class GroupAbility
    {
        public string group;
        public string name;
        public string description;
        public string trigger;
        public string effect;
        public float value;
        public float cooldown_s;
        public string visual_cue;
        public string chemistry_basis;
    }

    [Serializable]
    public class GroupAbilityList
    {
        public GroupAbility[] abilities;
    }

    /// <summary>Loads Resources/AtomIO/Content/group_abilities.json once and looks abilities up by group.</summary>
    public static class GroupAbilityDatabase
    {
        private const string ResourcePath = "AtomIO/Content/group_abilities";
        private static Dictionary<string, GroupAbility> byGroup;

        public static GroupAbility ForGroup(string group)
        {
            Load();
            return group != null && byGroup.TryGetValue(group, out var ability) ? ability : null;
        }

        private static void Load()
        {
            if (byGroup != null)
            {
                return;
            }

            byGroup = new Dictionary<string, GroupAbility>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("[Atom.IO] group_abilities.json not found in Resources/AtomIO/Content.");
                return;
            }

            var list = JsonUtility.FromJson<GroupAbilityList>(asset.text);
            if (list?.abilities == null)
            {
                return;
            }

            foreach (var a in list.abilities)
            {
                if (!string.IsNullOrEmpty(a.group))
                {
                    byGroup[a.group] = a;
                }
            }
        }
    }
}
