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
        private const float MaxShieldSeconds = 5f;

        [SerializeField] private float baseMoveSpeed = 6f;
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private SpriteRenderer rimRenderer;
        [SerializeField] private float contactDamageCooldown = 0.5f;
        [SerializeField] private float pulseAmplitude = 0.06f;

        [Header("Movement feel")]
        [Tooltip("Stick response: 1 = linear, >1 = small tilts move slowly for precise control.")]
        [SerializeField, Range(1f, 3f)] private float stickCurve = 1.6f;
        [Tooltip("How fast the atom reaches its target speed (units/s per second) as Hydrogen.")]
        [SerializeField] private float acceleration = 30f;
        [Tooltip("How fast it slows down when you let go of the stick.")]
        [SerializeField] private float deceleration = 20f;
        [Tooltip("Extra inertia per proton: heavier atoms speed up and stop more slowly (Neon = 1 + 9 x this).")]
        [SerializeField] private float massPerProton = 0.09f;
        [Tooltip("Speed multiplier while the aim stick is held.")]
        [SerializeField, Range(0.2f, 1f)] private float aimSpeedMultiplier = 0.6f;
        [Tooltip("Backwards kick (units/s) when an electron is thrown, divided by the atom's mass.")]
        [SerializeField] private float throwRecoil = 5f;

        public event Action<int, int> HealthChanged; // current, max
        public event Action Died;

        public int AtomicNumber { get; private set; } = 1;
        public int MaxHp { get; private set; } = 10;
        public int CurrentHp { get; private set; } = 10;
        public bool IsDead => CurrentHp <= 0;
        public TierConfig Config { get; private set; }

        /// <summary>True while a timed ability shield blocks contact damage. Always time-limited.</summary>
        public bool IsShielded => Time.time < shieldUntil;

        private Rigidbody2D body;
        private Vector2 moveInput;
        private Vector2 velocity;     // smoothed velocity (momentum)
        private bool aiming;
        private float aimBlend;       // eases in and out of the aiming slowdown
        private float moveSpeedMult = 1f;
        private float lastHitTime = -999f;
        private float baseScale = 1f;
        private string pattern = "solid";
        private float shieldUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        /// <summary>Applies a level's stats and look. Called at run start and on every evolution
        /// (HP refills to the new maximum).</summary>
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

        /// <summary>Inertia: 1 for Hydrogen, growing with every proton.</summary>
        public float Mass => 1f + massPerProton * Mathf.Max(0, AtomicNumber - 1);

        /// <summary>Current movement velocity (after momentum and recoil).</summary>
        public Vector2 Velocity => velocity;

        /// <summary>Aim stick held: move slower while aiming.</summary>
        public void SetAiming(bool isAiming)
        {
            aiming = isAiming;
        }

        /// <summary>Throwing an electron kicks the atom the opposite way (lighter atoms fly further).</summary>
        public void ApplyThrowRecoil(Vector2 throwDirection)
        {
            if (IsDead || throwDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            velocity -= throwDirection.normalized * (throwRecoil / Mass);
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

            float dt = Time.fixedDeltaTime;

            // Analog curve: light tilts are slow and precise, full tilt is full speed.
            float tilt = moveInput.magnitude;
            Vector2 input = tilt > 0.0001f ? moveInput / tilt * Mathf.Pow(tilt, stickCurve) : Vector2.zero;

            // Aiming trades speed for accuracy (eased so it doesn't feel like a brake).
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, dt * 6f);
            float maxSpeed = baseMoveSpeed * moveSpeedMult * Mathf.Lerp(1f, aimSpeedMultiplier, aimBlend);

            // Momentum: accelerate toward the target velocity; heavier atoms respond more slowly.
            float mass = Mass;
            Vector2 desired = input * maxSpeed;
            float rate = (input.sqrMagnitude > 0.0001f ? acceleration : deceleration) / mass;
            // Turning around uses the stronger of the two so reversing still feels responsive.
            if (Vector2.Dot(desired, velocity) < 0f)
            {
                rate = Mathf.Max(acceleration, deceleration) / mass;
            }

            velocity = Vector2.MoveTowards(velocity, desired, rate * dt);
            body.linearVelocity = velocity;

            // Keep the atom inside the circular arena (and lose the speed pushing outward).
            if (Config != null && Config.arena != null)
            {
                float r = Config.arena.radius - 0.5f;
                if (body.position.sqrMagnitude > r * r)
                {
                    Vector2 n = body.position.normalized;
                    body.position = n * r;
                    float outward = Vector2.Dot(velocity, n);
                    if (outward > 0f)
                    {
                        velocity -= n * outward;
                        body.linearVelocity = velocity;
                    }
                }
            }
        }

        /// <summary>Called by enemies touching the core. Has a short cooldown per hit.</summary>
        public void TakeContactDamage(int amount)
        {
            if (IsDead || IsShielded || Time.time - lastHitTime < contactDamageCooldown)
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

        /// <summary>Restores HP (capped at the maximum). Used by group abilities.</summary>
        public void Heal(int amount)
        {
            if (IsDead || amount <= 0 || CurrentHp >= MaxHp)
            {
                return;
            }

            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
            HealthChanged?.Invoke(CurrentHp, MaxHp);
        }

        /// <summary>Blocks contact damage for a short, capped time. Never permanent.</summary>
        public void GrantShield(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float until = Time.time + Mathf.Min(seconds, MaxShieldSeconds);
            shieldUntil = Mathf.Max(shieldUntil, until);
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

            // Core grows with the nucleus: Neon reads clearly bigger than Hydrogen.
            pattern = string.IsNullOrEmpty(visual.pattern) ? "solid" : visual.pattern;
            baseScale = 1f + 0.06f * (AtomicNumber - 1);
            transform.localScale = new Vector3(baseScale, baseScale, 1f);
        }

        private void Update()
        {
            // Light per-pattern animation; the Level Designer's shader notes are the full version.
            if (pattern == "pulse")
            {
                float s = baseScale * (1f + pulseAmplitude * Mathf.Sin(Time.time * 4f));
                transform.localScale = new Vector3(s, s, 1f);
            }
            else if (rimRenderer != null && (pattern == "rings" || pattern == "swirl"))
            {
                rimRenderer.transform.Rotate(0f, 0f, (pattern == "swirl" ? 180f : 60f) * Time.deltaTime);
            }
        }
    }
}
