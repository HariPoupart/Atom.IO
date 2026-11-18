using System.Collections.Generic;
using AtomIO.Core;
using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Energy pickup: a small white dot holding a fixed amount of energy (AtomFormulas.OrbEnergy).
    /// Spawned naturally around the arena, or dropped in a scattered burst when an enemy dies.
    /// Collected on contact. Orbs from an enemy killed by a THROWN electron home in on the player.
    ///
    /// Performance: orbs are POOLED (re-used instead of Instantiate/Destroy, so no garbage-collection
    /// spikes when dozens drop at once), tracked in <see cref="Active"/> (so nobody has to call
    /// FindObjectsByType), and have a kinematic Rigidbody2D so moving them doesn't rebuild a static collider.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnergyOrb : MonoBehaviour
    {
        [SerializeField] private float dotScale = 0.3f;
        [SerializeField] private float homingStartSpeed = 4f;
        [SerializeField] private float homingAcceleration = 40f;
        [SerializeField] private float homingMaxSpeed = 45f;

        /// <summary>Every orb currently in the arena (pooled orbs are removed while inactive).</summary>
        public static readonly List<EnergyOrb> Active = new List<EnergyOrb>();

        private static readonly Stack<EnergyOrb> Pool = new Stack<EnergyOrb>();
        private static EnergyOrb template;

        private float value = AtomFormulas.OrbEnergy;
        private Transform homingTarget;
        private float homingSpeed;
        private bool collected;
        private Rigidbody2D body;

        public float Value => value;
        public bool IsHoming => homingTarget != null;

        private void Awake()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
            body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.None;

            // White dot look (also applied to prefabs built before this change).
            transform.localScale = new Vector3(dotScale, dotScale, 1f);
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.color = Color.white;
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
            value = AtomFormulas.OrbEnergy;
            collected = false;
            homingTarget = null;
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Kept for compatibility; orbs hold a fixed amount of energy.</summary>
        public void SetValue(float v)
        {
            value = Mathf.Max(0f, v);
        }

        /// <summary>Fly to the target from any distance and get collected on arrival.</summary>
        public void HomeTo(Transform target)
        {
            homingTarget = target;
            homingSpeed = homingStartSpeed;
        }

        /// <summary>Takes an orb from the pool (or creates one) at <paramref name="position"/>.</summary>
        public static EnergyOrb Spawn(EnergyOrb prefab, Vector3 position, Transform homeTo = null)
        {
            if (prefab != null)
            {
                template = prefab;
            }

            EnergyOrb orb = null;
            while (orb == null && Pool.Count > 0)
            {
                orb = Pool.Pop(); // entries destroyed by a scene change come back as null: skip them
            }

            if (orb != null)
            {
                orb.transform.position = position;
                orb.gameObject.SetActive(true);
            }
            else if (template != null)
            {
                orb = Instantiate(template, position, Quaternion.identity);
            }

            if (orb != null && homeTo != null)
            {
                orb.HomeTo(homeTo);
            }

            return orb;
        }

        /// <summary>
        /// Drops enough fixed-value orbs to hold <paramref name="energy"/>, scattered around
        /// <paramref name="center"/>. If <paramref name="homeTo"/> is set they all fly to it.
        /// Returns the number of orbs spawned.
        /// </summary>
        public static int SpawnBurst(EnergyOrb prefab, Vector3 center, float energy, Transform homeTo,
                                     float radius = 1.4f)
        {
            if (prefab == null && template == null)
            {
                return 0;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(energy / AtomFormulas.OrbEnergy), 1, AtomFormulas.MaxOrbsPerKill);
            for (int i = 0; i < count; i++)
            {
                Spawn(prefab, center + (Vector3)(Random.insideUnitCircle * radius), homeTo);
            }

            return count;
        }

        private void Update()
        {
            if (homingTarget == null || collected)
            {
                return;
            }

            homingSpeed = Mathf.Min(homingMaxSpeed, homingSpeed + homingAcceleration * Time.deltaTime);
            Vector2 target = homingTarget.position;
            Vector2 next = Vector2.MoveTowards(transform.position, target, homingSpeed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if ((target - next).sqrMagnitude < 0.25f)
            {
                Collect();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var rb = other.attachedRigidbody;
            if (rb != null && rb.TryGetComponent<PlayerAtom>(out _))
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            var session = ArenaSession.Instance;
            if (session != null)
            {
                session.AddEnergy(value);
            }

            Release();
        }

        /// <summary>Back to the pool instead of Destroy (no garbage, no allocation next time).</summary>
        private void Release()
        {
            homingTarget = null;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
