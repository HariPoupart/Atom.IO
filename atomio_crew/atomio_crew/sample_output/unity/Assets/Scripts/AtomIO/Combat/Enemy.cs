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

        public event Action<Enemy> Killed;

        public float Hp { get; private set; }

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

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
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
                return;
            }

            if (Time.time >= nextWanderChange)
            {
                wanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
                nextWanderChange = Time.time + UnityEngine.Random.Range(1.5f, 3.5f);
            }

            body.linearVelocity = wanderDirection * moveSpeed * wanderSpeedFactor;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            var player = collision.collider.GetComponentInParent<PlayerAtom>();
            if (player != null)
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
        }

        public void TakeDamage(int damage)
        {
            if (Hp <= 0f)
            {
                return;
            }

            Hp -= damage;
            if (Hp <= 0f)
            {
                if (orbPrefab != null)
                {
                    var orb = Instantiate(orbPrefab, transform.position, Quaternion.identity);
                    orb.SetValue(dropValue);
                }

                Killed?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
