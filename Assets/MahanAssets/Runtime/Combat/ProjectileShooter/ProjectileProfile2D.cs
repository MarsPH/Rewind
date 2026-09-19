using UnityEngine;

namespace TimeEcho
{
    public enum ProjectileColliderShape
    {
        Circle,
        Box
    }

    public enum ProjectileTargetRule
    {
        PlayerOnly,
        AnyDamageable
    }

    public enum ProjectileTemporalResponse
    {
        Freeze,
        DespawnOnRewind,
        DespawnOnAnyTemporalControl
    }

    [CreateAssetMenu(fileName = "Projectile Profile 2D", menuName = "Time Echo/Combat/Projectile Profile 2D")]
    public sealed class ProjectileProfile2D : ScriptableObject
    {
        [Header("Appearance")]
        [Tooltip("Optional name used in runtime instances and debugging.")]
        public string displayName = "Enemy Projectile";
        [Tooltip("Leave empty to keep the sprite already placed on the projectile prefab.")]
        public Sprite sprite;
        public Color tint = new Color(1f, 0.35f, 0.2f, 1f);
        [Tooltip("Local scale of the projectile's Visual child, independent of its collider.")]
        public Vector2 visualScale = new Vector2(0.32f, 0.18f);
        [Tooltip("Leave empty to retain the material from the projectile prefab.")]
        public Material spriteMaterial;
        public string sortingLayerName = "Default";
        public int sortingOrder = 10;
        public bool rotateVisualTowardsVelocity = true;
        public float visualRotationOffset;
        [Tooltip("Visual-only spin in degrees per second. It can be combined with movement-facing rotation.")]
        public float visualSpinSpeed;

        [Header("Trail")]
        public bool useTrail = true;
        [Min(0f)] public float trailTime = 0.22f;
        [Min(0f)] public float trailStartWidth = 0.12f;
        [Min(0f)] public float trailEndWidth;
        public Gradient trailColor;
        [Tooltip("Leave empty to retain the Trail Renderer material from the prefab.")]
        public Material trailMaterial;

        [Header("Movement")]
        [Min(0f)] public float speed = 8f;
        [Tooltip("Units per second added to speed every second. Negative values slow the projectile.")]
        public float acceleration;
        [Tooltip("0 means no maximum beyond the launch speed.")]
        [Min(0f)] public float maximumSpeed;
        public float gravityScale;
        [Min(0.05f)] public float lifetime = 6f;

        [Header("Optional Homing")]
        public bool homing;
        [Min(0f)] public float homingDelay;
        [Tooltip("0 means the projectile keeps homing until it expires or hits something.")]
        [Min(0f)] public float homingDuration;
        [Min(0f)] public float homingTurnSpeed = 180f;

        [Header("Collision Shape")]
        public ProjectileColliderShape colliderShape = ProjectileColliderShape.Circle;
        [Min(0.001f)] public float circleRadius = 0.13f;
        public Vector2 boxSize = new Vector2(0.28f, 0.16f);
        public Vector2 colliderOffset;
        [Tooltip("Only colliders on these layers can be hit. This is checked even if the Physics 2D layer matrix permits contact.")]
        public LayerMask collisionLayers = ~0;
        [Tooltip("Enable only when the projectile is supposed to hit trigger volumes too.")]
        public bool hitTriggerColliders;

        [Header("Damage")]
        public ProjectileTargetRule targetRule = ProjectileTargetRule.PlayerOnly;
        [Min(0f)] public float damage = 25f;
        [Min(0f)] public float knockback = 4f;
        [Tooltip("How many additional damageable targets the projectile may pass through after the first.")]
        [Min(0)] public int extraTargetPierces;
        [Tooltip("Deactivate when a valid collision is not damageable, such as a wall or floor.")]
        public bool deactivateOnEnvironmentHit = true;

        [Header("Time Echo Integration")]
        [Tooltip("Dynamic projectiles are not automatically registered for rewind. Choose whether they freeze or are removed when temporal control starts.")]
        public ProjectileTemporalResponse temporalResponse = ProjectileTemporalResponse.DespawnOnRewind;

        [Header("Effects")]
        public AudioCue impactCue;
        public GameObject spawnEffectPrefab;
        public GameObject impactEffectPrefab;
        [Min(0.05f)] public float spawnedEffectLifetime = 2f;

        private void OnEnable()
        {
            EnsureTrailGradient();
            Sanitize();
        }

        private void OnValidate()
        {
            EnsureTrailGradient();
            Sanitize();
        }

        public void Sanitize()
        {
            visualScale.x = Mathf.Max(0.001f, Mathf.Abs(visualScale.x));
            visualScale.y = Mathf.Max(0.001f, Mathf.Abs(visualScale.y));
            trailTime = Mathf.Max(0f, trailTime);
            trailStartWidth = Mathf.Max(0f, trailStartWidth);
            trailEndWidth = Mathf.Max(0f, trailEndWidth);
            speed = Mathf.Max(0f, speed);
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            lifetime = Mathf.Max(0.05f, lifetime);
            homingDelay = Mathf.Max(0f, homingDelay);
            homingDuration = Mathf.Max(0f, homingDuration);
            homingTurnSpeed = Mathf.Max(0f, homingTurnSpeed);
            circleRadius = Mathf.Max(0.001f, circleRadius);
            boxSize.x = Mathf.Max(0.001f, Mathf.Abs(boxSize.x));
            boxSize.y = Mathf.Max(0.001f, Mathf.Abs(boxSize.y));
            damage = Mathf.Max(0f, damage);
            knockback = Mathf.Max(0f, knockback);
            extraTargetPierces = Mathf.Max(0, extraTargetPierces);
            spawnedEffectLifetime = Mathf.Max(0.05f, spawnedEffectLifetime);
        }

        private void EnsureTrailGradient()
        {
            if (trailColor != null)
            {
                return;
            }

            trailColor = new Gradient();
            trailColor.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.15f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
    }
}
