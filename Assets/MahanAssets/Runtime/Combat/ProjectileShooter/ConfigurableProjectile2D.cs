using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ConfigurableProjectile2D : MonoBehaviour
    {
        [Header("Prefab References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private CircleCollider2D circleCollider;
        [SerializeField] private BoxCollider2D boxCollider;
        [SerializeField] private TrailRenderer trail;

        private readonly HashSet<int> damagedTargets = new HashSet<int>();
        private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[16];

        private ProjectileProfile2D profile;
        private GameObject owner;
        private Transform homingTarget;
        private TimeDirector observedTimeDirector;
        private ContactFilter2D sweepFilter;
        private float age;
        private float visualSpin;
        private int remainingPierces;
        private bool launched;

        public ProjectileProfile2D Profile => profile;
        public GameObject Owner => owner;
        public bool IsLaunched => launched && gameObject.activeSelf;
        public bool HasRequiredReferences => body != null && visual != null && spriteRenderer != null &&
                                             circleCollider != null && boxCollider != null;

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void OnEnable()
        {
            if (launched)
            {
                BindTimeDirector();
            }
        }

        private void Update()
        {
            if (!launched || profile == null)
            {
                return;
            }

            if (observedTimeDirector == null)
            {
                BindTimeDirector();
            }

            age += Time.deltaTime;
            if (age >= profile.lifetime)
            {
                Recall();
                return;
            }

            visualSpin += profile.visualSpinSpeed * Time.deltaTime;
            UpdateVisualRotation();
        }

        private void FixedUpdate()
        {
            if (!launched || profile == null || body == null)
            {
                return;
            }

            if (observedTimeDirector != null && observedTimeDirector.Mode != TimeMode.Flowing)
            {
                return;
            }

            UpdateMotion();
            SweepAhead();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!launched || other == null)
            {
                return;
            }

            Vector2 direction = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
                : (Vector2)transform.right;
            Vector2 point = other.ClosestPoint(transform.position);
            ProcessCollider(other, point, direction);
        }

        private void OnDisable()
        {
            UnbindTimeDirector();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (trail != null)
            {
                trail.Clear();
            }

            launched = false;
        }

        private void OnDestroy()
        {
            UnbindTimeDirector();
        }

        public void ConfigureReferences(
            Rigidbody2D projectileBody,
            Transform visualTransform,
            SpriteRenderer renderer,
            CircleCollider2D circle,
            BoxCollider2D box,
            TrailRenderer trailRenderer)
        {
            body = projectileBody;
            visual = visualTransform;
            spriteRenderer = renderer;
            circleCollider = circle;
            boxCollider = box;
            trail = trailRenderer;
        }

        public void Launch(
            ProjectileProfile2D projectileProfile,
            GameObject projectileOwner,
            Transform target,
            Vector2 position,
            Vector2 direction,
            Vector2 inheritedVelocity)
        {
            if (projectileProfile == null)
            {
                Debug.LogError("A projectile cannot launch without a Projectile Profile 2D.", this);
                return;
            }

            ResolveMissingReferences();
            if (!HasRequiredReferences)
            {
                Debug.LogError("The configurable projectile prefab is missing one or more required references.", this);
                return;
            }

            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            profile = projectileProfile;
            owner = projectileOwner;
            homingTarget = target;
            age = 0f;
            visualSpin = 0f;
            remainingPierces = profile.extraTargetPierces;
            damagedTargets.Clear();
            launched = true;
            if (!string.IsNullOrWhiteSpace(profile.displayName))
            {
                gameObject.name = profile.displayName + " (Pooled)";
            }

            ApplyProfile();
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            gameObject.SetActive(true);

            body.linearVelocity = direction * profile.speed + inheritedVelocity;
            body.angularVelocity = 0f;
            if (trail != null)
            {
                trail.Clear();
            }

            SpawnEffect(profile.spawnEffectPrefab, position, transform.rotation);
            BindTimeDirector();
        }

        public void SetHomingTarget(Transform target)
        {
            homingTarget = target;
        }

        public void Recall()
        {
            if (!gameObject.activeSelf)
            {
                launched = false;
                return;
            }

            launched = false;
            gameObject.SetActive(false);
        }

        private void ResolveMissingReferences()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (visual == null)
            {
                Transform candidate = transform.Find("Visual");
                visual = candidate != null ? candidate : transform;
            }
            if (spriteRenderer == null) spriteRenderer = visual.GetComponent<SpriteRenderer>();
            if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
            if (trail == null) trail = GetComponent<TrailRenderer>();
        }

        private void ApplyProfile()
        {
            if (profile.sprite != null)
            {
                spriteRenderer.sprite = profile.sprite;
            }
            spriteRenderer.color = profile.tint;
            if (profile.spriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = profile.spriteMaterial;
            }
            if (!string.IsNullOrWhiteSpace(profile.sortingLayerName))
            {
                spriteRenderer.sortingLayerName = profile.sortingLayerName;
            }
            spriteRenderer.sortingOrder = profile.sortingOrder;
            visual.localScale = new Vector3(profile.visualScale.x, profile.visualScale.y, 1f);
            visual.localRotation = Quaternion.Euler(0f, 0f, profile.visualRotationOffset);

            circleCollider.enabled = profile.colliderShape == ProjectileColliderShape.Circle;
            circleCollider.isTrigger = true;
            circleCollider.radius = profile.circleRadius;
            circleCollider.offset = profile.colliderOffset;

            boxCollider.enabled = profile.colliderShape == ProjectileColliderShape.Box;
            boxCollider.isTrigger = true;
            boxCollider.size = profile.boxSize;
            boxCollider.offset = profile.colliderOffset;

            body.gravityScale = profile.gravityScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (trail != null)
            {
                trail.enabled = profile.useTrail;
                trail.time = profile.trailTime;
                trail.startWidth = profile.trailStartWidth;
                trail.endWidth = profile.trailEndWidth;
                if (profile.trailColor != null) trail.colorGradient = profile.trailColor;
                if (profile.trailMaterial != null) trail.sharedMaterial = profile.trailMaterial;
            }

            sweepFilter = new ContactFilter2D();
            sweepFilter.SetLayerMask(profile.collisionLayers);
            sweepFilter.useTriggers = profile.hitTriggerColliders;
        }

        private void UpdateMotion()
        {
            Vector2 velocity = body.linearVelocity;
            float speed = velocity.magnitude;

            bool insideHomingWindow = profile.homing && age >= profile.homingDelay &&
                                      (profile.homingDuration <= 0f || age <= profile.homingDelay + profile.homingDuration);
            if (insideHomingWindow && homingTarget != null)
            {
                Vector2 desired = (Vector2)homingTarget.position - body.position;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    float currentAngle = velocity.sqrMagnitude > 0.0001f
                        ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg
                        : transform.eulerAngles.z;
                    float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                    float nextAngle = Mathf.MoveTowardsAngle(
                        currentAngle,
                        desiredAngle,
                        profile.homingTurnSpeed * Time.fixedDeltaTime);
                    float radians = nextAngle * Mathf.Deg2Rad;
                    float retainedSpeed = speed > 0.001f ? speed : profile.speed;
                    velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * retainedSpeed;
                }
            }

            if (Mathf.Abs(profile.acceleration) > 0.0001f && velocity.sqrMagnitude > 0.0001f)
            {
                float acceleratedSpeed = Mathf.Max(0f, velocity.magnitude + profile.acceleration * Time.fixedDeltaTime);
                velocity = velocity.normalized * acceleratedSpeed;
            }

            if (profile.maximumSpeed > 0f && velocity.magnitude > profile.maximumSpeed)
            {
                velocity = velocity.normalized * profile.maximumSpeed;
            }

            body.linearVelocity = velocity;
        }

        private void SweepAhead()
        {
            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float distance = velocity.magnitude * Time.fixedDeltaTime + 0.02f;
            int count = body.Cast(velocity.normalized, sweepFilter, sweepHits, distance);
            for (int i = 0; i < count && launched; i++)
            {
                RaycastHit2D hit = sweepHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                ProcessCollider(hit.collider, hit.point, velocity.normalized);
            }
        }

        private void ProcessCollider(Collider2D other, Vector2 hitPoint, Vector2 hitDirection)
        {
            if (!launched || profile == null || other == null || IsOwnerCollider(other))
            {
                return;
            }

            int layerBit = 1 << other.gameObject.layer;
            if ((profile.collisionLayers.value & layerBit) == 0)
            {
                return;
            }
            if (other.isTrigger && !profile.hitTriggerColliders)
            {
                return;
            }

            if (TryFindDamageable(other, out IDamageable damageable, out Component damageableComponent))
            {
                int targetId = damageableComponent.gameObject.GetInstanceID();
                if (!damagedTargets.Add(targetId))
                {
                    return;
                }

                Vector2 direction = hitDirection.sqrMagnitude > 0.0001f
                    ? hitDirection.normalized
                    : ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
                bool applied = profile.damage > 0f && damageable.ApplyDamage(profile.damage, hitPoint, direction);
                if (applied && profile.knockback > 0f)
                {
                    Rigidbody2D targetBody = damageableComponent.GetComponentInParent<Rigidbody2D>();
                    if (targetBody != null && targetBody != body)
                    {
                        targetBody.AddForce(direction * profile.knockback, ForceMode2D.Impulse);
                    }
                }

                PlayImpact(hitPoint, direction);
                if (remainingPierces > 0)
                {
                    remainingPierces--;
                    return;
                }

                Recall();
                return;
            }

            if (profile.deactivateOnEnvironmentHit)
            {
                PlayImpact(hitPoint, hitDirection);
                Recall();
            }
        }

        private bool TryFindDamageable(Collider2D other, out IDamageable damageable, out Component component)
        {
            damageable = null;
            component = null;

            if (profile.targetRule == ProjectileTargetRule.PlayerOnly)
            {
                PlayerMotor2D motor = other.GetComponentInParent<PlayerMotor2D>();
                if (motor == null)
                {
                    return false;
                }

                PlayerVitality vitality = motor.GetComponent<PlayerVitality>();
                if (vitality == null)
                {
                    return false;
                }

                damageable = vitality;
                component = vitality;
                return true;
            }

            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable found)
                {
                    damageable = found;
                    component = behaviours[i];
                    return true;
                }
            }

            return false;
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            if (owner == null)
            {
                return false;
            }

            Transform ownerTransform = owner.transform;
            return other.gameObject == owner ||
                   other.transform.IsChildOf(ownerTransform);
        }

        private void UpdateVisualRotation()
        {
            if (visual == null || profile == null)
            {
                return;
            }

            if (profile.rotateVisualTowardsVelocity && body != null && body.linearVelocity.sqrMagnitude > 0.0001f)
            {
                float movementAngle = Mathf.Atan2(body.linearVelocity.y, body.linearVelocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, movementAngle);
                visual.localRotation = Quaternion.Euler(0f, 0f, profile.visualRotationOffset + visualSpin);
                return;
            }

            visual.localRotation = Quaternion.Euler(0f, 0f, profile.visualRotationOffset + visualSpin);
        }

        private void PlayImpact(Vector2 position, Vector2 direction)
        {
            AudioService.Instance?.Play(profile.impactCue, position);
            float angle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : transform.eulerAngles.z;
            SpawnEffect(profile.impactEffectPrefab, position, Quaternion.Euler(0f, 0f, angle));
        }

        private void SpawnEffect(GameObject prefab, Vector2 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            Destroy(instance, profile != null ? profile.spawnedEffectLifetime : 2f);
        }

        private void BindTimeDirector()
        {
            TimeDirector director = TimeDirector.Instance;
            if (observedTimeDirector == director)
            {
                return;
            }

            UnbindTimeDirector();
            observedTimeDirector = director;
            if (observedTimeDirector != null)
            {
                observedTimeDirector.ModeChanged += HandleTimeModeChanged;
            }
        }

        private void UnbindTimeDirector()
        {
            if (observedTimeDirector != null)
            {
                observedTimeDirector.ModeChanged -= HandleTimeModeChanged;
                observedTimeDirector = null;
            }
        }

        private void HandleTimeModeChanged(TimeMode previous, TimeMode next)
        {
            if (!launched || profile == null)
            {
                return;
            }

            bool despawn = profile.temporalResponse == ProjectileTemporalResponse.DespawnOnAnyTemporalControl &&
                           next != TimeMode.Flowing;
            despawn |= profile.temporalResponse == ProjectileTemporalResponse.DespawnOnRewind &&
                       next == TimeMode.Rewinding;
            if (despawn)
            {
                Recall();
            }
        }
    }
}
