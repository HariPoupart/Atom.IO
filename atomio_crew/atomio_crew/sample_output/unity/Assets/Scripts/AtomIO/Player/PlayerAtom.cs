using System;
using AtomIO.Data;
using UnityEngine;

namespace AtomIO.Player
{
    /// <summary>
    /// The player's nucleus: movement, health (10 HP per proton) and core visuals.
    /// Needs a Rigidbody2D (gravity 0) and a CircleCollider2D on the "Player" layer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerAtom : MonoBehaviour
    {
        [SerializeField] private float baseMoveSpeed = 6f;
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private SpriteRenderer rimRenderer;
        [SerializeField] private float contactDamageCooldown = 0.5f;

        public event Action<int, int> HealthChanged; // current, max
        public event Action Died;

        public int AtomicNumber { get; private set; } = 1;
        public int MaxHp { get; private set; } = 10;
        public int CurrentHp { get; private set; } = 10;
        public bool IsDead => CurrentHp <= 0;
        public TierConfig Config { get; private set; }

        private Rigidbody2D body;
        private Vector2 moveInput;
        private float moveSpeedMult = 1f;
        private float lastHitTime = -999f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        public void Init(TierConfig config)
        {
            Config = config;
            AtomicNumber = config.atomic_number;
            moveSpeedMult = config.trait != null ? config.trait.move_speed_mult : 1f;
            float hpMult = config.trait != null ? config.trait.max_hp_mult : 1f;
            MaxHp = Mathf.Max(1, Mathf.RoundToInt(config.player.max_hp * hpMult));
            CurrentHp = MaxHp;
            ApplyVisuals(config.core_visual);
            HealthChanged?.Invoke(CurrentHp, MaxHp);
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
            if (IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = moveInput * baseMoveSpeed * moveSpeedMult;

            // Keep the atom inside the circular arena.
            if (Config != null && Config.arena != null)
            {
                float r = Config.arena.radius;
                if (body.position.sqrMagnitude > r * r)
                {
                    body.position = body.position.normalized * r;
                }
            }
        }

        /// <summary>Called by enemies touching the core. Has a short cooldown per hit.</summary>
        public void TakeContactDamage(int amount)
        {
            if (IsDead || Time.time - lastHitTime < contactDamageCooldown)
            {
                return;
            }

            lastHitTime = Time.time;
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            HealthChanged?.Invoke(CurrentHp, MaxHp);
            if (CurrentHp == 0)
            {
                Died?.Invoke();
            }
        }

        private void ApplyVisuals(TierCoreVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            if (coreRenderer != null)
            {
                coreRenderer.color = TierConfig.ParseHex(visual.primary_hex, Color.white);
            }

            if (rimRenderer != null)
            {
                Color rim = TierConfig.ParseHex(visual.secondary_hex, Color.cyan);
                rim.a = Mathf.Clamp01(0.35f + 0.2f * visual.glow_intensity);
                rimRenderer.color = rim;
            }

            // Core grows slowly with the nucleus so heavy atoms read as "big".
            float scale = 1f + 0.01f * AtomicNumber;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
