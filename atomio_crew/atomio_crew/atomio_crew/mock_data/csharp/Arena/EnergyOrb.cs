using AtomIO.Core;
using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Energy pickup: a small white dot holding a fixed amount of energy (AtomFormulas.OrbEnergy).
    /// Spawned naturally around the arena, or dropped in a scattered burst when an enemy dies.
    /// Collected on contact. Orbs from an enemy killed by a THROWN electron home in on the player
    /// from any distance ("sucked in").
    /// Prefab: SpriteRenderer + CircleCollider2D (isTrigger).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnergyOrb : MonoBehaviour
    {
        [SerializeField] private float dotScale = 0.3f;
        [SerializeField] private float homingStartSpeed = 4f;
        [SerializeField] private float homingAcceleration = 40f;
        [SerializeField] private float homingMaxSpeed = 45f;

        private float value = AtomFormulas.OrbEnergy;
        private Transform homingTarget;
        private float homingSpeed;
        private bool collected;

        public float Value => value;
        public bool IsHoming => homingTarget != null;

        private void Awake()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
            value = AtomFormulas.OrbEnergy;

            // White dot look (also applied to prefabs built before this change).
            transform.localScale = new Vector3(dotScale, dotScale, 1f);
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.color = Color.white;
            }
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

        /// <summary>
        /// Drops enough fixed-value orbs to hold <paramref name="energy"/>, scattered around
        /// <paramref name="center"/>. If <paramref name="homeTo"/> is set they all fly to it.
        /// Returns the number of orbs spawned.
        /// </summary>
        public static int SpawnBurst(EnergyOrb prefab, Vector3 center, float energy, Transform homeTo,
                                     float radius = 1.4f)
        {
            if (prefab == null)
            {
                return 0;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(energy / AtomFormulas.OrbEnergy), 1, AtomFormulas.MaxOrbsPerKill);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = center + (Vector3)(Random.insideUnitCircle * radius);
                var orb = Instantiate(prefab, pos, Quaternion.identity);
                if (homeTo != null)
                {
                    orb.HomeTo(homeTo);
                }
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
            Vector3 target = homingTarget.position;
            Vector3 next = Vector3.MoveTowards(transform.position, target, homingSpeed * Time.deltaTime);
            next.z = transform.position.z;
            transform.position = next;
            Vector2 gap = (Vector2)(target - next);
            if (gap.sqrMagnitude < 0.25f)
            {
                Collect();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerAtom>() != null)
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

            Destroy(gameObject);
        }
    }
}
