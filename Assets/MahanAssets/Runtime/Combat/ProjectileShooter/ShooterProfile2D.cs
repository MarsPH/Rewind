using UnityEngine;

namespace TimeEcho
{
    public enum ShooterTargetingMode
    {
        FindPlayer,
        AssignedOnly,
        AssignedThenPlayer
    }

    public enum ShooterFireMode
    {
        Automatic,
        OncePerTargetEntry,
        ManualOnly
    }

    public enum ShooterVolleyPattern
    {
        Single,
        Spread,
        Radial
    }

    [CreateAssetMenu(fileName = "Shooter Profile 2D", menuName = "Time Echo/Combat/Shooter Profile 2D")]
    public sealed class ShooterProfile2D : ScriptableObject
    {
        [Header("Activation")]
        public bool beginActive = true;
        [Tooltip("Delay after activation before the automatic schedule may begin.")]
        [Min(0f)] public float initialDelay = 0.5f;
        [Tooltip("When enabled, the shooter does nothing during rewind or stasis.")]
        public bool pauseOutsideFlowingTime = true;

        [Header("Targeting")]
        public ShooterTargetingMode targetingMode = ShooterTargetingMode.AssignedThenPlayer;
        [Min(0.02f)] public float targetRefreshInterval = 0.25f;
        [Min(0.1f)] public float acquisitionRange = 18f;
        [Tooltip("Kept at or above Acquisition Range so a target does not rapidly flicker in and out.")]
        [Min(0.1f)] public float loseTargetRange = 21f;
        [Min(0f)] public float minimumFireRange;
        [Min(0.1f)] public float maximumFireRange = 15f;
        public Vector2 targetOffset;
        public bool requireLineOfSight;
        [Tooltip("Layers that block a shot. Leave empty when Require Line Of Sight is off.")]
        public LayerMask lineOfSightObstacleLayers;

        [Header("Aiming")]
        [Tooltip("How fast the Aim Pivot turns. 0 snaps instantly.")]
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("Local direction used as the front of the art. 0 = right, 90 = up, 180 = left.")]
        public float visualForwardAngle;
        public bool requireAimAlignment = true;
        [Range(0f, 180f)] public float aimTolerance = 4f;
        [Tooltip("When enabled, bullets follow the current pivot direction. This makes Turn Speed and Aim Tolerance meaningful.")]
        public bool fireAlongCurrentAim = true;
        public bool returnToIdleWithoutTarget;
        public float idleLocalAngle;

        [Header("Prediction")]
        [Tooltip("Leads a moving Rigidbody2D target using the projectile's initial speed. Best for linear, low-gravity projectiles.")]
        public bool predictTargetMovement;
        [Min(0f)] public float maximumPredictionTime = 2f;

        [Header("Fire Schedule")]
        public ShooterFireMode fireMode = ShooterFireMode.Automatic;
        [Tooltip("Telegraph time before each configured burst begins.")]
        [Min(0f)] public float windupTime = 0.25f;
        [Min(1)] public int shotsPerBurst = 1;
        [Min(0.01f)] public float timeBetweenBurstShots = 0.12f;
        [Min(0f)] public float cooldownAfterBurst = 1.25f;
        [Tooltip("A random value in this range is added to Cooldown After Burst. Useful for less mechanical timing.")]
        public Vector2 additionalRandomCooldown;
        public bool cancelWindupWhenTargetLost = true;
        public bool cancelBurstWhenTargetLost = true;

        [Header("Volley")]
        public ShooterVolleyPattern volleyPattern = ShooterVolleyPattern.Single;
        [Min(1)] public int projectilesPerVolley = 1;
        [Range(0f, 360f)] public float spreadAngle = 25f;
        [Tooltip("For Spread, choose independent random angles instead of evenly spaced angles.")]
        public bool randomizeSpread;
        [Range(0f, 45f)] public float randomAimError;
        [Range(0f, 1f)] public float inheritedShooterVelocity;

        [Header("Pooling")]
        [Min(0)] public int prewarmCount = 12;
        [Tooltip("0 means unlimited. When the cap is reached, the shot is skipped instead of allocating another projectile.")]
        [Min(0)] public int maximumPoolSize = 48;

        [Header("Feedback")]
        public AudioCue targetAcquiredCue;
        public AudioCue windupCue;
        public AudioCue fireCue;
        public GameObject muzzleFlashPrefab;
        [Min(0.05f)] public float muzzleFlashLifetime = 1f;
        public string windupAnimatorTrigger = "Windup";
        public string fireAnimatorTrigger = "Fire";

        private void OnEnable()
        {
            Sanitize();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        public void Sanitize()
        {
            initialDelay = Mathf.Max(0f, initialDelay);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            acquisitionRange = Mathf.Max(0.1f, acquisitionRange);
            loseTargetRange = Mathf.Max(acquisitionRange, loseTargetRange);
            minimumFireRange = Mathf.Max(0f, minimumFireRange);
            maximumFireRange = Mathf.Max(Mathf.Max(0.1f, minimumFireRange), maximumFireRange);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            aimTolerance = Mathf.Clamp(aimTolerance, 0f, 180f);
            maximumPredictionTime = Mathf.Max(0f, maximumPredictionTime);
            windupTime = Mathf.Max(0f, windupTime);
            shotsPerBurst = Mathf.Max(1, shotsPerBurst);
            timeBetweenBurstShots = Mathf.Max(0.01f, timeBetweenBurstShots);
            cooldownAfterBurst = Mathf.Max(0f, cooldownAfterBurst);
            projectilesPerVolley = Mathf.Max(1, projectilesPerVolley);
            spreadAngle = Mathf.Clamp(spreadAngle, 0f, 360f);
            randomAimError = Mathf.Clamp(randomAimError, 0f, 45f);
            prewarmCount = Mathf.Max(0, prewarmCount);
            maximumPoolSize = Mathf.Max(0, maximumPoolSize);
            if (maximumPoolSize > 0)
            {
                prewarmCount = Mathf.Min(prewarmCount, maximumPoolSize);
            }
            muzzleFlashLifetime = Mathf.Max(0.05f, muzzleFlashLifetime);
        }
    }
}
