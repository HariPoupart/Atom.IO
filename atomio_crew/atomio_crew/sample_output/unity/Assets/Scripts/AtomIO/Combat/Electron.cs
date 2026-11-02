using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Combat
{
    public enum ElectronState
    {
        Wild,      // idle in the arena, can be collected
        Orbiting,  // circling the player, deals orbit damage on touch
        Released   // fired by the player, deals release damage once
    }

    /// <summary>
    /// One electron. Prefab: SpriteRenderer + CircleCollider2D (isTrigger) + kinematic Rigidbody2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Electron : MonoBehaviour
    {
        [SerializeField] private float releaseSpeed = 14f;
        [SerializeField] private float releaseLifetime = 1.6f;
        [SerializeField] private TrailRenderer trail;

        public ElectronState State { get; private set; } = ElectronState.Wild;

        private Rigidbody2D body;
        private float damageMult = 1f;
        private int orbitDamage = 10;
        private int releaseDamage = 20;
        private float releasedAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            GetComponent<CircleCollider2D>().isTrigger = true;
            SetTrail(false);
        }

        public void AttachTo(ElectronOrbit orbit, int orbitDmg, int releaseDmg, float mult)
        {
            orbitDamage = orbitDmg;
            releaseDamage = releaseDmg;
            damageMult = mult;
            State = ElectronState.Orbiting;
            body.linearVelocity = Vector2.zero;
            SetTrail(false);
        }

        /// <summary>Called by the orbit controller every frame while orbiting.</summary>
        public void SetOrbitPosition(Vector2 worldPosition)
        {
            body.MovePosition(worldPosition);
        }

        public void Release(Vector2 direction)
        {
            State = ElectronState.Released;
            releasedAt = Time.time;
            body.linearVelocity = direction.normalized * releaseSpeed;
            SetTrail(true);
        }

        public void MakeWild()
        {
            State = ElectronState.Wild;
            body.linearVelocity = Vector2.zero;
            SetTrail(false);
        }

        private void Update()
        {
            // A released electron that misses slows down and becomes collectable again.
            if (State == ElectronState.Released && Time.time - releasedAt > releaseLifetime)
            {
                MakeWild();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (State == ElectronState.Wild)
            {
                var orbit = other.GetComponentInParent<ElectronOrbit>();
                if (orbit != null)
                {
                    orbit.TryCollect(this);
                }

                return;
            }

            var enemy = other.GetComponentInParent<Enemy>();
            if (enemy == null)
            {
                return;
            }

            if (State == ElectronState.Released)
            {
                enemy.TakeDamage(Mathf.RoundToInt(releaseDamage * damageMult));
                Destroy(gameObject);
            }
            else
            {
                enemy.TryTakeOrbitHit(Mathf.RoundToInt(orbitDamage * damageMult));
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (State != ElectronState.Orbiting)
            {
                return;
            }

            var enemy = other.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.TryTakeOrbitHit(Mathf.RoundToInt(orbitDamage * damageMult));
            }
        }

        private void SetTrail(bool on)
        {
            if (trail != null)
            {
                trail.emitting = on;
            }
        }
    }
}
