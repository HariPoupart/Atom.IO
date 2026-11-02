using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Arena
{
    /// <summary>
    /// Energy pickup, spawned naturally or dropped by enemies. Collected on contact.
    /// Prefab: SpriteRenderer (+ optional particle glow) + CircleCollider2D (isTrigger).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnergyOrb : MonoBehaviour
    {
        [SerializeField] private float value = 1f;

        public float Value => value;

        private void Awake()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        public void SetValue(float v)
        {
            value = Mathf.Max(0f, v);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerAtom>() == null)
            {
                return;
            }

            var session = ArenaSession.Instance;
            if (session != null)
            {
                session.AddEnergy(value);
            }

            Destroy(gameObject);
        }
    }
}
