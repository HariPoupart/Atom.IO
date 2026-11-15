using AtomIO.Arena;
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
    /// One electron, drawn as a glowing yellow sphere (core + white-hot centre + soft halo).
    /// Wild electrons bob and pulse so they read as "loose", live for the level's lifetime_s,
    /// then fade out and go back to the pool. Collecting one plays a short flash.
    /// Prefab: SpriteRenderer + CircleCollider2D (isTrigger) + kinematic Rigidbody2D, halo child.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Electron : MonoBehaviour
    {
        [SerializeField] private float releaseSpeed = 14f;
        [SerializeField] private float releaseLifetime = 1.6f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private Color color = new Color(1f, 0.847f, 0.302f); // #FFD84D
        [SerializeField] private float haloAlphaWild = 0.55f;
        [SerializeField] private float haloAlphaOrbit = 0.3f;
        [SerializeField] private float wildBob = 0.08f;
        [SerializeField] private float wildPulse = 0.1f;
        [SerializeField] private float wildPulseHz = 1.5f;
        [SerializeField] private float defaultWildLifetime = 25f;
        [SerializeField] private float fadeSeconds = 1.5f;
        [SerializeField] private float flashSeconds = 0.2f;
        [SerializeField] private AudioClip pickupClip;

        public ElectronState State { get; private set; } = ElectronState.Wild;

        private Rigidbody2D rb;
        private ElectronPool pool;
        private bool countedWild;
        private float damageMult = 1f;
        private int orbitDamage = 10;
        private int releaseDamage = 20;
        private float releasedAt;
        private float wildSince;
        private float wildLifetime = 25f;
        private Vector3 wildAnchor;
        private Vector3 baseScale = Vector3.one;
        private float flashStart = -999f;
        private float phase;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None; // the orbit sets positions itself every frame
            GetComponent<CircleCollider2D>().isTrigger = true;
            baseScale = transform.localScale;
            phase = Random.Range(0f, 6.28f);
            SetTrail(false);
        }

        /// <summary>Called by the pool each time this electron is reused.</summary>
        public void ResetForSpawn(ElectronPool owner)
        {
            pool = owner;
            countedWild = false;
            State = ElectronState.Orbiting; // the caller sets the real state right after
            rb.linearVelocity = Vector2.zero;
            transform.localScale = baseScale;
            SetTrail(false);
            SetLook(1f, haloAlphaOrbit);
        }

        public void AttachTo(ElectronOrbit orbit, int orbitDmg, int releaseDmg, float mult)
        {
            orbitDamage = orbitDmg;
            releaseDamage = releaseDmg;
            damageMult = mult;
            SetState(ElectronState.Orbiting);
            rb.linearVelocity = Vector2.zero;
            transform.localScale = baseScale;
            SetTrail(false);
            SetLook(1f, haloAlphaOrbit);
        }

        /// <summary>Flash and sound when the player collects this electron.</summary>
        public void PlayPickupFeedback()
        {
            flashStart = Time.time;
            if (pickupClip != null)
            {
                AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.6f);
            }
        }

        /// <summary>Called by the orbit controller every rendered frame while orbiting (LateUpdate).
        /// Sets the position directly, like the demo script, so the orbit is smooth on screen;
        /// the kinematic body picks the new position up for triggers on the next physics step.</summary>
        public void SetOrbitPosition(Vector2 worldPosition)
        {
            transform.position = worldPosition;
        }

        public void Release(Vector2 direction)
        {
            SetState(ElectronState.Released);
            releasedAt = Time.time;
            rb.linearVelocity = direction.normalized * releaseSpeed;
            SetTrail(true);
        }

        /// <summary>Lies loose in the arena: bobs, pulses and fades after the level's lifetime.</summary>
        public void MakeWild()
        {
            SetState(ElectronState.Wild);
            rb.linearVelocity = Vector2.zero;
            SetTrail(false);
            wildSince = Time.time;
            wildAnchor = transform.position;
            wildLifetime = defaultWildLifetime;
            var session = ArenaSession.Instance;
            if (session != null && session.Config != null && session.Config.electrons != null)
            {
                wildLifetime = session.Config.electrons.lifetime_s;
            }

            SetLook(1f, haloAlphaWild);
        }

        /// <summary>Leaves the arena: back to the pool (or destroyed if there is no pool).</summary>
        public void Despawn()
        {
            SetCountedWild(false);
            if (pool != null)
            {
                pool.Return(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            switch (State)
            {
                case ElectronState.Released:
                    // A released electron that misses slows down and becomes collectable again.
                    if (Time.time - releasedAt > releaseLifetime)
                    {
                        MakeWild();
                    }

                    break;
                case ElectronState.Wild:
                    UpdateWild();
                    break;
                case ElectronState.Orbiting:
                    float t = (Time.time - flashStart) / flashSeconds;
                    float flash = t < 1f ? 1f + 0.6f * (1f - t) : 1f;
                    transform.localScale = baseScale * flash;
                    break;
            }
        }

        private void UpdateWild()
        {
            float age = Time.time - wildSince;
            if (wildLifetime > 0f && age >= wildLifetime)
            {
                Despawn();
                return;
            }

            float wave = Mathf.Sin((Time.time + phase) * wildPulseHz * 2f * Mathf.PI);
            transform.position = wildAnchor + new Vector3(0f, wave * wildBob, 0f);
            transform.localScale = baseScale * (1f + wildPulse * wave);
            float fade = wildLifetime > 0f ? Mathf.Clamp01((wildLifetime - age) / fadeSeconds) : 1f;
            SetLook(fade, haloAlphaWild * fade);
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
                Despawn();
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

        private void SetState(ElectronState next)
        {
            State = next;
            SetCountedWild(next == ElectronState.Wild);
        }

        private void SetCountedWild(bool wild)
        {
            if (wild == countedWild)
            {
                return;
            }

            countedWild = wild;
            if (pool != null)
            {
                pool.ChangeWildCount(wild ? 1 : -1);
            }
        }

        private void SetLook(float bodyAlpha, float haloAlpha)
        {
            if (body != null)
            {
                var c = color;
                c.a = bodyAlpha;
                body.color = c;
            }

            if (halo != null)
            {
                var h = color;
                h.a = haloAlpha;
                halo.color = h;
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
