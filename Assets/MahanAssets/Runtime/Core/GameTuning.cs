using System;
using UnityEngine;

namespace TimeEcho
{
    public enum MovementStyle
    {
        Platformer,
        TopDown
    }

    public enum AimedActionMode
    {
        LaunchPlayer,
        FireProjectile
    }

    public enum TimerPolicy
    {
        FlowingWorldTime,
        RealAttemptTime,
        RewindableTimeline
    }

    [CreateAssetMenu(fileName = "GameTuning", menuName = "Time Echo/Game Tuning")]
    public sealed class GameTuning : ScriptableObject
    {
        public MovementTuning movement = new MovementTuning();
        public AimTuning aim = new AimTuning();
        public RewindTuning rewind = new RewindTuning();
        public VitalityTuning vitality = new VitalityTuning();
        public CollectibleTuning collectible = new CollectibleTuning();
        public FeedbackTuning feedback = new FeedbackTuning();
        public TimerTuning timer = new TimerTuning();

        private void OnValidate()
        {
            Sanitize();
        }

        public void Sanitize()
        {
            movement.Sanitize();
            aim.Sanitize();
            rewind.Sanitize();
            vitality.Sanitize();
            // Upgrade pre-existing GameTuning assets that were created before shard rewards moved here.
            if (collectible == null) collectible = new CollectibleTuning();
            collectible.Sanitize();
            feedback.Sanitize();
        }
    }

    [Serializable]
    public sealed class MovementTuning
    {
        public MovementStyle style = MovementStyle.Platformer;
        [Min(0f)] public float maximumSpeed = 7f;
        [Min(0f)] public float acceleration = 60f;
        [Min(0f)] public float deceleration = 75f;
        [Range(0f, 1f)] public float airControl = 0.65f;
        [Min(0f)] public float gravityScale = 3f;
        public bool allowKeyboardJump = true;
        [Min(0f)] public float keyboardJumpSpeed = 11f;
        [Min(0f)] public float coyoteTime = 0.1f;
        [Min(0f)] public float jumpBufferTime = 0.12f;
        [Min(0.01f)] public float groundProbeRadius = 0.12f;
        public LayerMask groundLayers = ~0;

        internal void Sanitize()
        {
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            gravityScale = Mathf.Max(0f, gravityScale);
            keyboardJumpSpeed = Mathf.Max(0f, keyboardJumpSpeed);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
        }
    }

    [Serializable]
    public sealed class AimTuning
    {
        public AimedActionMode action = AimedActionMode.LaunchPlayer;
        [Min(0.1f), Tooltip("Minimum time to hold LMB before releasing can boost. The arrow appears immediately; quick clicks cancel without spending energy.")]
        public float holdThreshold = 0.18f;
        [Min(0.01f)] public float fullChargeTime = 0.75f;
        [Range(0f, 1f)] public float tapCharge = 0.55f;
        [Min(0f)] public float minimumImpulse = 8f;
        [Min(0f)] public float maximumImpulse = 18f;
        [Min(1f), Tooltip("Multiplier applied to existing velocity before adding the launch impulse. One preserves all momentum.")]
        public float retainedVelocity = 1f;
        [Range(0f, 1f), Tooltip("Fraction of maximum vitality spent by each successful player boost.")]
        public float boostVitalityCostFraction = 0.25f;
        [Min(0f), Tooltip("Seconds after launch before ordinary movement begins changing boost momentum.")]
        public float boostMomentumHoldSeconds = 0.2f;
        [Min(0f), Tooltip("Seconds used to smoothly return ordinary movement control after the momentum hold.")]
        public float boostControlRecoverySeconds = 0.25f;
        [Min(0f)] public float actionCooldown = 0.08f;
        [Min(0.01f)] public float pointerDeadZone = 0.1f;
        public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Arrow")]
        [Min(0.1f)] public float minimumArrowLength = 0.8f;
        [Min(0.1f)] public float maximumArrowLength = 4.5f;
        [Min(0.005f)] public float lineWidth = 0.08f;
        [Min(0.01f)] public float arrowHeadSize = 0.28f;
        public Color normalArrowColor = new Color(0.35f, 0.95f, 1f, 0.95f);
        public Color stasisArrowColor = new Color(1f, 0.82f, 0.25f, 1f);

        [Header("Projectile option")]
        [Min(0f)] public float minimumProjectileSpeed = 10f;
        [Min(0f)] public float maximumProjectileSpeed = 24f;

        internal void Sanitize()
        {
            holdThreshold = Mathf.Max(0.1f, holdThreshold);
            fullChargeTime = Mathf.Max(0.01f, fullChargeTime);
            minimumImpulse = Mathf.Max(0f, minimumImpulse);
            maximumImpulse = Mathf.Max(minimumImpulse, maximumImpulse);
            retainedVelocity = Mathf.Max(1f, retainedVelocity);
            boostVitalityCostFraction = Mathf.Clamp01(boostVitalityCostFraction);
            boostMomentumHoldSeconds = Mathf.Max(0f, boostMomentumHoldSeconds);
            boostControlRecoverySeconds = Mathf.Max(0f, boostControlRecoverySeconds);
            actionCooldown = Mathf.Max(0f, actionCooldown);
            pointerDeadZone = Mathf.Max(0.01f, pointerDeadZone);
            minimumArrowLength = Mathf.Max(0.1f, minimumArrowLength);
            maximumArrowLength = Mathf.Max(minimumArrowLength, maximumArrowLength);
            lineWidth = Mathf.Max(0.005f, lineWidth);
            arrowHeadSize = Mathf.Max(0.01f, arrowHeadSize);
            minimumProjectileSpeed = Mathf.Max(0f, minimumProjectileSpeed);
            maximumProjectileSpeed = Mathf.Max(minimumProjectileSpeed, maximumProjectileSpeed);
        }
    }

    [Serializable]
    public sealed class RewindTuning
    {
        [Min(0.1f)] public float historySeconds = 8f;
        [Min(0.05f)] public float playbackSpeed = 1.35f;
        [Min(0f)] public float vitalityCostPerSecond = 8f;
        [Min(0f), Tooltip("Legacy setting retained for existing assets. Stasis aiming is free; a successful stasis launch costs one normal boost.")]
        public float stasisVitalityCostPerSecond = 0f;
        [Min(0f)] public float minimumVitalityToStart = 1f;
        [Min(0f)] public float maximumStasisSeconds = 0f;

        internal void Sanitize()
        {
            historySeconds = Mathf.Max(0.1f, historySeconds);
            playbackSpeed = Mathf.Max(0.05f, playbackSpeed);
            vitalityCostPerSecond = Mathf.Max(0f, vitalityCostPerSecond);
            stasisVitalityCostPerSecond = Mathf.Max(0f, stasisVitalityCostPerSecond);
            minimumVitalityToStart = Mathf.Max(0f, minimumVitalityToStart);
            maximumStasisSeconds = Mathf.Max(0f, maximumStasisSeconds);
        }
    }

    [Serializable]
    public sealed class VitalityTuning
    {
        [Min(1f)] public float maximum = 100f;
        public bool rewindCanReduceToZero = true;
        public bool boostCanReduceToZero = true;

        internal void Sanitize()
        {
            maximum = Mathf.Max(1f, maximum);
        }
    }

    [Serializable]
    public sealed class CollectibleTuning
    {
        [Range(0f, 1f), Tooltip("Fraction of maximum energy restored by each Time Shard. 0.25 restores 25 energy when max is 100; 0 disables restoration. Applies to all shards using the player's GameTuning.")]
        public float energyRestoreFraction = 0.25f;

        internal void Sanitize()
        {
            energyRestoreFraction = Mathf.Clamp01(energyRestoreFraction);
        }
    }

    [Serializable]
    public sealed class FeedbackTuning
    {
        [Min(0.05f)] public float footstepInterval = 0.28f;
        [Min(0f)] public float landingSpeedThreshold = 3f;
        [Range(0f, 1f)] public float rewindOverlayAlpha = 0.16f;
        [Range(0f, 1f)] public float stasisOverlayAlpha = 0.12f;

        internal void Sanitize()
        {
            footstepInterval = Mathf.Max(0.05f, footstepInterval);
            landingSpeedThreshold = Mathf.Max(0f, landingSpeedThreshold);
        }
    }

    [Serializable]
    public sealed class TimerTuning
    {
        public TimerPolicy policy = TimerPolicy.FlowingWorldTime;
        public bool beginOnSceneLoad = true;
    }
}
