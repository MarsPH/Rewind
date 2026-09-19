using System;
using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameInput input;
        [SerializeField] private TimeDirector timeDirector;
        [SerializeField] private Transform groundProbe;

        private readonly Collider2D[] groundResults = new Collider2D[8];
        private Rigidbody2D body;
        private Collider2D ownCollider;
        private PlayerVitality vitality;
        private Vector2 desiredMove;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastJumpPressedTime = float.NegativeInfinity;
        private float nextLaunchTime;
        private float launchControlLockedUntil;
        private float launchControlFullyRestoredAt;
        private bool preserveLaunchMomentumInAir;
        private bool hasLeftGroundSinceLaunch;
        private float stepTimer;
        private float previousVerticalVelocity;
        private bool wasGrounded;
        private bool dead;

        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public float FacingSign { get; private set; } = 1f;
        public bool IsDead => dead;

        public event Action Jumped;
        public event Action<float> Landed;
        public event Action<Vector2, float> Launched;
        public event Action Footstep;
        public event Action<float> FacingChanged;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
            vitality = GetComponent<PlayerVitality>();

            if (tuning != null)
            {
                body.gravityScale = tuning.movement.style == MovementStyle.Platformer
                    ? tuning.movement.gravityScale
                    : 0f;
            }

            if (input == null)
            {
                input = FindObjectOfType<GameInput>();
            }

            if (timeDirector == null)
            {
                timeDirector = FindObjectOfType<TimeDirector>();
            }
        }

        private void OnEnable()
        {
            if (vitality == null) vitality = GetComponent<PlayerVitality>();
            if (vitality != null)
            {
                vitality.Died += OnDied;
                vitality.Revived += OnRevived;
            }
        }

        private void OnDisable()
        {
            if (vitality != null)
            {
                vitality.Died -= OnDied;
                vitality.Revived -= OnRevived;
            }
        }

        private void Update()
        {
            bool locked = PresentationDirector.Instance != null && PresentationDirector.Instance.InputLocked;
            bool canMove = !dead && !locked && (timeDirector == null || timeDirector.Mode == TimeMode.Flowing);
            desiredMove = canMove && input != null ? input.Current.Move : Vector2.zero;

            if (desiredMove.x != 0f && Mathf.Sign(desiredMove.x) != FacingSign)
            {
                FacingSign = Mathf.Sign(desiredMove.x);
                FacingChanged?.Invoke(FacingSign);
            }

            if (canMove && input != null && input.Current.JumpPressed)
            {
                lastJumpPressedTime = Time.time;
            }
        }

        private void FixedUpdate()
        {
            if (timeDirector != null && timeDirector.Mode != TimeMode.Flowing)
            {
                return;
            }

            UpdateGroundedState();
            if (dead)
            {
                return;
            }

            MovementTuning movement = tuning != null ? tuning.movement : null;
            MovementStyle style = movement != null ? movement.style : MovementStyle.Platformer;
            if (style == MovementStyle.TopDown)
            {
                ApplyTopDownMovement(movement);
            }
            else
            {
                ApplyPlatformerMovement(movement);
            }

            UpdateFootsteps(movement);
            previousVerticalVelocity = body.linearVelocity.y;
            wasGrounded = IsGrounded;
        }

        public void Configure(GameTuning gameTuning, GameInput gameInput, TimeDirector director, Transform probe)
        {
            tuning = gameTuning;
            input = gameInput;
            timeDirector = director;
            groundProbe = probe;
        }

        public bool TryLaunch(Vector2 direction, float impulse)
        {
            return TryLaunch(direction, impulse, false);
        }

        public bool TryLaunch(Vector2 direction, float impulse, bool alignVelocityToAim)
        {
            if (dead || body == null || Time.unscaledTime < nextLaunchTime || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            bool launchedFromGround = IsGrounded;
            float retainedVelocity = tuning != null ? Mathf.Max(1f, tuning.aim.retainedVelocity) : 1f;
            // Ordinary boosts retain momentum. Frozen-time boosts instead
            // launch along the actual aim arrow even if the player entered
            // stasis with velocity in a different direction.
            Vector2 boostVelocity = direction.normalized * Mathf.Max(0f, impulse);
            body.linearVelocity = alignVelocityToAim
                ? boostVelocity
                : body.linearVelocity * retainedVelocity + boostVelocity;
            nextLaunchTime = Time.unscaledTime + (tuning != null ? tuning.aim.actionCooldown : 0.08f);

            float holdSeconds = tuning != null ? tuning.aim.boostMomentumHoldSeconds : 0.2f;
            float recoverySeconds = tuning != null ? tuning.aim.boostControlRecoverySeconds : 0.25f;
            launchControlLockedUntil = Time.time + Mathf.Max(0f, holdSeconds);
            launchControlFullyRestoredAt = launchControlLockedUntil + Mathf.Max(0f, recoverySeconds);
            preserveLaunchMomentumInAir = true;
            hasLeftGroundSinceLaunch = !launchedFromGround;
            IsGrounded = false;
            // A probe may still overlap the ground during the first take-off
            // frame. Do not mistake that overlap for a fresh landing.
            wasGrounded = launchedFromGround;
            Launched?.Invoke(direction.normalized, impulse);
            return true;
        }

        public void SetDead(bool value)
        {
            dead = value;
            desiredMove = Vector2.zero;
            if (value)
            {
                ClearLaunchMomentumProtection();
            }
        }

        private void OnDied()
        {
            SetDead(true);
        }

        private void OnRevived()
        {
            SetDead(false);
        }

        private void ApplyPlatformerMovement(MovementTuning movement)
        {
            float maxSpeed = movement != null ? movement.maximumSpeed : 7f;
            float acceleration = movement != null ? movement.acceleration : 60f;
            float deceleration = movement != null ? movement.deceleration : 75f;
            float control = IsGrounded ? 1f : (movement != null ? movement.airControl : 0.65f);
            control *= GetLaunchControlMultiplier();
            float targetX = desiredMove.x * maxSpeed;
            float rate = Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;
            float nextX = body.linearVelocity.x;
            bool airborneBoost = preserveLaunchMomentumInAir && !IsGrounded;
            bool steering = Mathf.Abs(desiredMove.x) > 0.01f;
            bool alreadyFasterInSteeringDirection = steering &&
                Mathf.Sign(desiredMove.x) == Mathf.Sign(nextX) &&
                Mathf.Abs(nextX) >= maxSpeed;

            // Do not brake a launched player back toward zero just because no
            // movement key is pressed. A key in the existing travel direction
            // must not clamp a fast boost back down to normal running speed.
            // Opposite-direction input can still steer after control recovers.
            if (!airborneBoost || (steering && !alreadyFasterInSteeringDirection))
            {
                nextX = Mathf.MoveTowards(nextX, targetX, rate * control * Time.fixedDeltaTime);
            }

            body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);

            bool allowJump = movement == null || movement.allowKeyboardJump;
            float coyote = movement != null ? movement.coyoteTime : 0.1f;
            float buffer = movement != null ? movement.jumpBufferTime : 0.12f;
            if (allowJump && Time.time - lastGroundedTime <= coyote && Time.time - lastJumpPressedTime <= buffer)
            {
                float jumpSpeed = movement != null ? movement.keyboardJumpSpeed : 11f;
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
                Jumped?.Invoke();
            }
        }

        private void ApplyTopDownMovement(MovementTuning movement)
        {
            float maxSpeed = movement != null ? movement.maximumSpeed : 7f;
            float acceleration = movement != null ? movement.acceleration : 60f;
            float deceleration = movement != null ? movement.deceleration : 75f;
            Vector2 target = desiredMove * maxSpeed;
            float rate = (target.sqrMagnitude > 0.001f ? acceleration : deceleration) * GetLaunchControlMultiplier();
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        private void UpdateGroundedState()
        {
            if (groundProbe == null)
            {
                IsGrounded = false;
                return;
            }

            float radius = tuning != null ? tuning.movement.groundProbeRadius : 0.12f;
            int mask = tuning != null ? tuning.movement.groundLayers.value : Physics2D.DefaultRaycastLayers;
            int count = Physics2D.OverlapCircleNonAlloc(groundProbe.position, radius, groundResults, mask);
            IsGrounded = false;
            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = groundResults[i];
                if (candidate == null || candidate == ownCollider || candidate.isTrigger || candidate.attachedRigidbody == body)
                {
                    continue;
                }

                IsGrounded = true;
                break;
            }

            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
            }

            bool justLanded = !wasGrounded && IsGrounded;
            if (preserveLaunchMomentumInAir && !IsGrounded)
            {
                hasLeftGroundSinceLaunch = true;
            }

            if (justLanded && (!preserveLaunchMomentumInAir || hasLeftGroundSinceLaunch))
            {
                ClearLaunchMomentumProtection();
            }

            else if (preserveLaunchMomentumInAir && IsGrounded &&
                     !hasLeftGroundSinceLaunch && Time.time >= launchControlFullyRestoredAt)
            {
                // Horizontal boosts can remain grounded for their entire
                // duration. Restore ordinary running after the launch window.
                ClearLaunchMomentumProtection();
            }

            if (justLanded && previousVerticalVelocity < 0f)
            {
                float landingSpeed = -previousVerticalVelocity;
                float threshold = tuning != null ? tuning.feedback.landingSpeedThreshold : 3f;
                if (landingSpeed >= threshold)
                {
                    Landed?.Invoke(landingSpeed);
                }
            }
        }

        private void UpdateFootsteps(MovementTuning movement)
        {
            if (!IsGrounded || Mathf.Abs(body.linearVelocity.x) < 0.2f || desiredMove.sqrMagnitude < 0.01f)
            {
                stepTimer = 0f;
                return;
            }

            float baseInterval = tuning != null ? tuning.feedback.footstepInterval : 0.28f;
            float speedRatio = Mathf.Clamp(Mathf.Abs(body.linearVelocity.x) / Mathf.Max(0.1f, movement != null ? movement.maximumSpeed : 7f), 0.35f, 1.5f);
            stepTimer += Time.fixedDeltaTime * speedRatio;
            if (stepTimer >= baseInterval)
            {
                stepTimer -= baseInterval;
                Footstep?.Invoke();
            }
        }

        private float GetLaunchControlMultiplier()
        {
            if (Time.time < launchControlLockedUntil)
            {
                return 0f;
            }

            if (launchControlFullyRestoredAt <= launchControlLockedUntil ||
                Time.time >= launchControlFullyRestoredAt)
            {
                return 1f;
            }

            return Mathf.InverseLerp(launchControlLockedUntil, launchControlFullyRestoredAt, Time.time);
        }

        private void ClearLaunchMomentumProtection()
        {
            preserveLaunchMomentumInAir = false;
            hasLeftGroundSinceLaunch = false;
            launchControlLockedUntil = float.NegativeInfinity;
            launchControlFullyRestoredAt = float.NegativeInfinity;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundProbe == null)
            {
                return;
            }

            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundProbe.position, tuning != null ? tuning.movement.groundProbeRadius : 0.12f);
        }
    }
}
