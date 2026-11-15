using System.Collections.Generic;
using AtomIO.Combat;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Re-uses electron objects instead of Instantiate / Destroy, so a long run makes no
    /// garbage-collection spikes on a 2 GB Android phone. Also counts how many electrons are
    /// wild right now (spawned ones and missed throws), which the spawner caps per level.
    /// </summary>
    public class ElectronPool : MonoBehaviour
    {
        public static ElectronPool Instance { get; private set; }

        [SerializeField] private Electron electronPrefab;
        [SerializeField] private int prewarm = 24;

        private readonly Stack<Electron> free = new Stack<Electron>();

        /// <summary>Electrons currently lying loose in the arena.</summary>
        public int WildCount { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (electronPrefab == null)
            {
                Debug.LogError("[Atom.IO] ElectronPool has no Electron prefab. Run Atom.IO > Build Game Scenes " +
                               "or assign Assets/AtomIO/Prefabs/Electron in the Arena object.");
            }

            for (int i = 0; i < prewarm && electronPrefab != null; i++)
            {
                var e = Create();
                e.gameObject.SetActive(false);
                free.Push(e);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Takes an electron from the pool (or makes one) and places it at <paramref name="position"/>.
        /// Returns null if the pool has no prefab.</summary>
        public Electron Get(Vector2 position)
        {
            var e = free.Count > 0 ? free.Pop() : Create();
            if (e == null)
            {
                return null;
            }

            e.transform.position = position;
            e.gameObject.SetActive(true);
            e.ResetForSpawn(this);
            return e;
        }

        public void Return(Electron e)
        {
            if (e == null || !e.gameObject.activeSelf)
            {
                return;
            }

            e.gameObject.SetActive(false);
            free.Push(e);
        }

        /// <summary>Called by electrons when they enter (+1) or leave (-1) the wild state.</summary>
        public void ChangeWildCount(int delta)
        {
            WildCount = Mathf.Max(0, WildCount + delta);
        }

        private Electron Create()
        {
            return electronPrefab != null
                ? Instantiate(electronPrefab, Vector3.zero, Quaternion.identity, transform)
                : null;
        }
    }
}
