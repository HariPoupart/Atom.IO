using System;
using AtomIO.Arena;
using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Combat
{
    /// <summary>
    /// Chases the player while in sight range, gives up when the player escapes,
    /// damages the core on contact and drops an energy orb on death.
    /// Prefab: SpriteRenderer + CircleCollider2D + dynamic Rigidbody2D (gravity 0) on the "Enemy" layer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private float orbitHitCooldown = 0.4f;
        [SerializeField] private float giveUpRangeMultiplier = 1.5f;
        [SerializeField] private float wanderSpeedFactor = 0.35f;
        [SerializeField] private EnergyOrb orbPrefab;
        [Header("Hit reaction (orbiting electrons)")]
        [SerializeField] private float hitKnockback = 7f;     // pushed away from the player
        [SerializeField] private float hitStunSeconds = 0.25f; // stops chasing while knocked back
        [SerializeField] private float hitFlashSeconds = 0.12f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float edgeMargin = 0.6f;      // stays this far inside the arena edge

        public event Action<Enemy> Killed;

        /// <summary>Raised for any enemy that dies (used by group abilities).</summary>
        public static event Action<Enemy> AnyKilled;

        /// <summary>Raised after an orbiting electron lands a hit (enemy, damage dealt).</summary>
        public static event Action<Enemy, int> AnyOrbitHit;

        public float Hp { get; private set; }

        /// <summary>Every live enemy (so abilities never need FindObjectsByType).</summary>
        public static readonly System.Collections.Generic.List<Enemy> Active = new System.Collections.Generic.List<Enemy>();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private Rigidbody2D body;
        private PlayerAtom target;
        private float moveSpeed;
        private float sightRange;
        private int contactDamage;
        private float dropValue;
        private bool chasing;
        private float lastOrbitHit = -999f;
        private Vector2 wanderDirection;
        private float nextWanderChange;
        private SpriteRenderer sprite;
        private Color spriteColor;
        private Vector3 baseScale;
        private Vector2 knockVelocity;
        private float stunUntil = -1f;
        private float flashUntil = -1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            sprite = GetComponentInChildren<SpriteRenderer>();
            body.interpolation = RigidbodyInterpolation2D.Interpolate; // smooth on screen between physics steps
            baseScale = transform.localScale;
        }

        public void Init(PlayerAtom player, float hp, float speed, float sight, int damage, float orbValue)
        {
            target = player;
            Hp = hp;
            moveSpeed = speed;
            sightRange = sight;
            contactDamage = damage;
            dropValue = orbValue;
        }

        private void FixedUpdate()
        {
            if (target == null || target.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (Time.time < stunUntil)
            {
                // Knocked back by an orbiting electron: slide away, slowing down, then resume.
                body.linearVelocity = knockVelocity;
                knockVelocity *= 0.85f;
                KeepInsideArena();
                return;
            }

            Vector2 toPlayer = (Vector2)target.transform.position - body.position;
            float dist = toPlayer.magnitude;

            // Hysteresis: start chasing inside sight range, give up only well outside it.
            if (!chasing && dist <= sightRange)
            {
                chasing = true;
            }
            else if (chasing && dist > sightRange * giveUpRangeMultiplier)
            {
                chasing = false;
            }

            if (chasing)
            {
                body.linearVelocity = toPlayer.normalized * moveSpeed;
                KeepInsideArena();
                return;
            }

            if (Time.time >= nextWanderChange)
            {
                wanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
                nextWanderChange = Time.time + UnityEngine.Random.Range(1.5f, 3.5f);
            }

            body.linearVelocity = wanderDirection * moveSpeed * wanderSpeedFactor;
            KeepInsideArena();
        }

        /// <summary>The arena is a circle centred on the origin: never let an enemy leave it.
        /// An enemy at the edge is put back inside, loses its outward speed, and a wanderer turns around.</summary>
        private void KeepInsideArena()
        {
            var session = ArenaSession.Instance;
            if (session == null || session.Config == null || session.Config.arena == null)
            {
                return;
            }

            float r = Mathf.Max(1f, session.Config.arena.radius - edgeMargin);
            Vector2 p = body.position;
            if (p.sqrMagnitude <= r * r)
            {
                return;
            }

            Vector2 n = p.normalized;
            body.position = n * r;
            transform.position = new Vector3(n.x * r, n.y * r, transform.position.z);
            Vector2 v = body.linearVelocity;
            float outward = Vector2.Dot(v, n);
            if (outward > 0f)
            {
                body.linearVelocity = v - n * outward;
            }

            knockVelocity -= n * Mathf.Max(0f, Vector2.Dot(knockVelocity, n));
            if (Vector2.Dot(wanderDirection, n) > 0f)
            {
                wanderDirection = -n;
            }
        }

        private void Update()
        {
            // Hit feedback: white flash and a quick squash while recently hit.
            if (sprite != null && Time.time >= flashUntil && flashUntil > 0f)
            {
                sprite.color = spriteColor;
                transform.localScale = baseScale;
                flashUntil = -1f;
            }
        }

        /// <summary>Reaction to an orbiting-electron hit: flash, squash, knockback away from the player
        /// and a short stun so the enemy visibly bounces off the electron shell.</summary>
        private void ReactToHit()
        {
            if (sprite != null)
            {
                if (flashUntil < 0f)
                {
                    spriteColor = sprite.color; // remember the real colour and size (they may be set after spawn)
                    baseScale = transform.localScale;
                }

                sprite.color = hitFlashColor;
                transform.localScale = new Vector3(baseScale.x * 1.2f, baseScale.y * 0.8f, baseScale.z);
            }

            flashUntil = Time.time + hitFlashSeconds;
            Vector2 away = target != null ? body.position - (Vector2)target.transform.position : UnityEngine.Random.insideUnitCircle;
            knockVelocity = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * hitKnockback;
            stunUntil = Time.time + hitStunSeconds;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            var rb = collision.rigidbody; // cheaper than GetComponentInParent every physics step
            if (rb != null && rb.TryGetComponent<PlayerAtom>(out var player))
            {
                player.TakeContactDamage(contactDamage);
            }
        }

        /// <summary>Orbiting electrons hit repeatedly, so they share a short cooldown.</summary>
        public void TryTakeOrbitHit(int damage)
        {
            if (Time.time - lastOrbitHit < orbitHitCooldown)
            {
                return;
            }

            lastOrbitHit = Time.time;
            TakeDamage(damage);
            AnyOrbitHit?.Invoke(this, damage);
            if (Hp > 0f)
            {
                ReactToHit();
            }
        }

        public void TakeDamage(int damage) => TakeDamage(damage, false);

        /// <summary>Damage the enemy. A kill drops scattered orbs worth its energy; if the killing blow
        /// came from a thrown electron, those orbs fly straight to the player.</summary>
        public void TakeDamage(int damage, bool fromThrownElectron)
        {
            if (Hp <= 0f)
            {
                return;
            }

            Hp -= damage;
            if (Hp <= 0f)
            {
                Transform suckTo = fromThrownElectron && target != null ? target.transform : null;
                EnergyOrb.SpawnBurst(orbPrefab, transform.position, dropValue, suckTo);

                Killed?.Invoke(this);
                AnyKilled?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
