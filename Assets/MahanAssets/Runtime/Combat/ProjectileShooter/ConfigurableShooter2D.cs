using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho
{
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    public sealed class ConfigurableShooter2D : MonoBehaviour
    {
        private enum FireState
        {
            Waiting,
            Windup,
            Burst
        }

        [Header("Profiles")]
        [SerializeField] private ShooterProfile2D shooterProfile;
        [SerializeField] private ProjectileProfile2D projectileProfile;

        [Header("Prefab References")]
        [SerializeField] private ConfigurableProjectile2D projectilePrefab;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Animator animator;

        [Header("Optional Scene Target")]
        [Tooltip("Used by Assigned Only and preferred by Assigned Then Player. It can also be assigned at runtime.")]
        [SerializeField] private Transform assignedTarget;

        [Header("Events")]
        [SerializeField] private UnityEvent onTargetAcquired = new UnityEvent();
        [SerializeField] private UnityEvent onTargetLost = new UnityEvent();
        [SerializeField] private UnityEvent onWindupStarted = new UnityEvent();
        [SerializeField] private UnityEvent onFired = new UnityEvent();
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

        private readonly List<ConfigurableProjectile2D> pool = new List<ConfigurableProjectile2D>();

        private Transform currentTarget;
        private Rigidbody2D currentTargetBody;
        private Rigidbody2D shooterBody;
        private FireState fireState;
        private Vector2 desiredAimDirection = Vector2.right;
        private float cooldownRemaining;
        private float stateTimer;
        private float targetRefreshRemaining;
        private int burstShotsRemaining;
        private bool firingActive;
        private bool wasInFireZone;
        private bool oncePerEntryConsumed;
        private bool manualBurstRequested;
        private bool configurationWarningLogged;

        public ShooterProfile2D ShooterProfile => shooterProfile;
        public ProjectileProfile2D ProjectileProfile => projectileProfile;
        public Transform CurrentTarget => currentTarget;
        public bool IsFiringActive => firingActive;
        public bool IsWindingUp => fireState == FireState.Windup;
        public bool HasRequiredReferences => shooterProfile != null && projectileProfile != null &&
                                             projectilePrefab != null && aimPivot != null && muzzle != null;
        public int ActiveProjectileCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        private void Reset()
        {
            ResolveMissingReferences();
        }

        private void Awake()
        {
            ResolveMissingReferences();
            shooterBody = GetComponentInParent<Rigidbody2D>();
            firingActive = shooterProfile != null && shooterProfile.beginActive;
            cooldownRemaining = shooterProfile != null ? shooterProfile.initialDelay : 0f;
            targetRefreshRemaining = 0f;

            if (shooterProfile != null)
            {
                PrewarmPool();
            }

            if (assignedTarget != null && shooterProfile != null &&
                shooterProfile.targetingMode != ShooterTargetingMode.FindPlayer)
            {
                SetCurrentTarget(assignedTarget);
            }
        }

        private void OnDisable()
        {
            CancelCurrentCycle();
            manualBurstRequested = false;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    Destroy(pool[i].gameObject);
                }
            }
            pool.Clear();
        }

        private void Update()
        {
            if (shooterProfile == null || projectileProfile == null || !firingActive)
            {
                return;
            }

            if (!TimeAllowsActions())
            {
                return;
            }

            float delta = Time.deltaTime;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - delta);
            targetRefreshRemaining -= delta;
            RefreshTargetIfNeeded();
            UpdateAim(delta);

            bool inFireZone = IsTargetInFireZone();
            if (!inFireZone)
            {
                if (wasInFireZone)
                {
                    oncePerEntryConsumed = false;
                }
                wasInFireZone = false;

                if ((fireState == FireState.Windup && shooterProfile.cancelWindupWhenTargetLost) ||
                    (fireState == FireState.Burst && shooterProfile.cancelBurstWhenTargetLost))
                {
                    CancelCurrentCycle();
                }
                return;
            }

            if (!wasInFireZone)
            {
                wasInFireZone = true;
                oncePerEntryConsumed = false;
            }

            bool aimReady = !shooterProfile.requireAimAlignment || IsAimAligned();
            UpdateFireState(delta, aimReady);
        }

        public void Configure(
            ShooterProfile2D behaviour,
            ProjectileProfile2D projectile,
            ConfigurableProjectile2D prefab,
            Transform pivot,
            Transform muzzleTransform,
            Animator optionalAnimator = null)
        {
            shooterProfile = behaviour;
            projectileProfile = projectile;
            projectilePrefab = prefab;
            aimPivot = pivot;
            muzzle = muzzleTransform;
            animator = optionalAnimator;
        }

        public void Activate()
        {
            if (firingActive)
            {
                return;
            }

            firingActive = true;
            cooldownRemaining = shooterProfile != null ? shooterProfile.initialDelay : 0f;
            targetRefreshRemaining = 0f;
            onActivated?.Invoke();
        }

        public void Deactivate()
        {
            if (!firingActive)
            {
                return;
            }

            firingActive = false;
            CancelCurrentCycle();
            manualBurstRequested = false;
            onDeactivated?.Invoke();
        }

        public void ToggleActive()
        {
            if (firingActive) Deactivate();
            else Activate();
        }

        public void DeactivateAndClearProjectiles()
        {
            Deactivate();
            RecallAllProjectiles();
        }

        public void RecallAllProjectiles()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    pool[i].Recall();
                }
            }
        }

        public void AssignTarget(Transform target)
        {
            assignedTarget = target;
            SetCurrentTarget(target);
            targetRefreshRemaining = shooterProfile != null ? shooterProfile.targetRefreshInterval : 0.25f;
        }

        public void ClearAssignedTarget()
        {
            assignedTarget = null;
            SetCurrentTarget(null);
            targetRefreshRemaining = 0f;
        }

        [ContextMenu("Fire One Volley Now")]
        public void FireOneVolleyNow()
        {
            if (!firingActive || shooterProfile == null || projectileProfile == null || !TimeAllowsActions())
            {
                return;
            }

            RefreshTargetIfNeeded(true);
            UpdateAim(0f);
            if (IsTargetInFireZone() && (!shooterProfile.requireAimAlignment || IsAimAligned()))
            {
                FireVolley();
            }
        }

        [ContextMenu("Start Configured Burst")]
        public void StartConfiguredBurst()
        {
            if (firingActive)
            {
                manualBurstRequested = true;
            }
        }

        private void UpdateFireState(float delta, bool aimReady)
        {
            if (fireState == FireState.Windup)
            {
                stateTimer = Mathf.Max(0f, stateTimer - delta);
                if (stateTimer <= 0f && aimReady)
                {
                    BeginBurst();
                }
                return;
            }

            if (fireState == FireState.Burst)
            {
                stateTimer = Mathf.Max(0f, stateTimer - delta);
                if (stateTimer <= 0f && aimReady)
                {
                    FireNextBurstShot();
                }
                return;
            }

            if (!aimReady)
            {
                return;
            }

            if (manualBurstRequested)
            {
                manualBurstRequested = false;
                BeginCycle();
                return;
            }

            if (cooldownRemaining > 0f)
            {
                return;
            }

            if (shooterProfile.fireMode == ShooterFireMode.Automatic)
            {
                BeginCycle();
            }
            else if (shooterProfile.fireMode == ShooterFireMode.OncePerTargetEntry && !oncePerEntryConsumed)
            {
                oncePerEntryConsumed = true;
                BeginCycle();
            }
        }

        private void BeginCycle()
        {
            if (shooterProfile.windupTime > 0f)
            {
                fireState = FireState.Windup;
                stateTimer = shooterProfile.windupTime;
                PlayWindupFeedback();
                return;
            }

            BeginBurst();
        }

        private void BeginBurst()
        {
            fireState = FireState.Burst;
            burstShotsRemaining = shooterProfile.shotsPerBurst;
            stateTimer = 0f;
            FireNextBurstShot();
        }

        private void FireNextBurstShot()
        {
            FireVolley();
            burstShotsRemaining--;
            if (burstShotsRemaining > 0)
            {
                fireState = FireState.Burst;
                stateTimer = shooterProfile.timeBetweenBurstShots;
                return;
            }

            CompleteCycle();
        }

        private void CompleteCycle()
        {
            fireState = FireState.Waiting;
            stateTimer = 0f;
            burstShotsRemaining = 0;

            float low = Mathf.Min(
                shooterProfile.additionalRandomCooldown.x,
                shooterProfile.additionalRandomCooldown.y);
            float high = Mathf.Max(
                shooterProfile.additionalRandomCooldown.x,
                shooterProfile.additionalRandomCooldown.y);
            cooldownRemaining = Mathf.Max(0f, shooterProfile.cooldownAfterBurst + Random.Range(low, high));
        }

        private void CancelCurrentCycle()
        {
            fireState = FireState.Waiting;
            stateTimer = 0f;
            burstShotsRemaining = 0;
        }

        private bool FireVolley()
        {
            if (projectilePrefab == null || projectileProfile == null)
            {
                LogConfigurationWarningOnce();
                return false;
            }

            Vector2 centreDirection = shooterProfile.fireAlongCurrentAim
                ? GetCurrentAimDirection()
                : desiredAimDirection;
            if (centreDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            centreDirection.Normalize();

            int projectileCount = shooterProfile.volleyPattern == ShooterVolleyPattern.Single
                ? 1
                : shooterProfile.projectilesPerVolley;
            Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
            Vector2 inheritedVelocity = shooterBody != null
                ? shooterBody.linearVelocity * shooterProfile.inheritedShooterVelocity
                : Vector2.zero;
            int spawned = 0;

            for (int i = 0; i < projectileCount; i++)
            {
                float angleOffset = GetVolleyAngle(i, projectileCount);
                if (shooterProfile.randomAimError > 0f)
                {
                    angleOffset += Random.Range(-shooterProfile.randomAimError, shooterProfile.randomAimError);
                }

                Vector2 direction = Rotate(centreDirection, angleOffset);
                ConfigurableProjectile2D projectile = GetAvailableProjectile();
                if (projectile == null)
                {
                    continue;
                }

                projectile.Launch(
                    projectileProfile,
                    gameObject,
                    currentTarget,
                    origin,
                    direction,
                    inheritedVelocity);
                spawned++;
            }

            if (spawned <= 0)
            {
                return false;
            }

            AudioService.Instance?.Play(shooterProfile.fireCue, origin);
            TriggerAnimator(shooterProfile.fireAnimatorTrigger);
            SpawnMuzzleFlash(origin, centreDirection);
            onFired?.Invoke();
            return true;
        }

        private float GetVolleyAngle(int index, int count)
        {
            if (shooterProfile.volleyPattern == ShooterVolleyPattern.Radial)
            {
                return count <= 1 ? 0f : 360f * index / count;
            }

            if (shooterProfile.volleyPattern != ShooterVolleyPattern.Spread || count <= 1)
            {
                return 0f;
            }

            float halfSpread = shooterProfile.spreadAngle * 0.5f;
            if (shooterProfile.randomizeSpread)
            {
                return Random.Range(-halfSpread, halfSpread);
            }

            float t = index / (float)(count - 1);
            return Mathf.Lerp(-halfSpread, halfSpread, t);
        }

        private void PlayWindupFeedback()
        {
            Vector3 position = muzzle != null ? muzzle.position : transform.position;
            AudioService.Instance?.Play(shooterProfile.windupCue, position);
            TriggerAnimator(shooterProfile.windupAnimatorTrigger);
            onWindupStarted?.Invoke();
        }

        private void TriggerAnimator(string triggerName)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        private void SpawnMuzzleFlash(Vector2 origin, Vector2 direction)
        {
            if (shooterProfile.muzzleFlashPrefab == null)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject effect = Instantiate(
                shooterProfile.muzzleFlashPrefab,
                origin,
                Quaternion.Euler(0f, 0f, angle));
            Destroy(effect, shooterProfile.muzzleFlashLifetime);
        }

        private void PrewarmPool()
        {
            if (projectilePrefab == null)
            {
                LogConfigurationWarningOnce();
                return;
            }

            for (int i = pool.Count; i < shooterProfile.prewarmCount; i++)
            {
                if (CreateProjectile() == null)
                {
                    break;
                }
            }
        }

        private ConfigurableProjectile2D GetAvailableProjectile()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].gameObject.activeSelf)
                {
                    return pool[i];
                }
            }

            return CreateProjectile();
        }

        private ConfigurableProjectile2D CreateProjectile()
        {
            if (projectilePrefab == null)
            {
                return null;
            }
            if (shooterProfile != null && shooterProfile.maximumPoolSize > 0 &&
                pool.Count >= shooterProfile.maximumPoolSize)
            {
                return null;
            }

            ConfigurableProjectile2D instance = Instantiate(projectilePrefab);
            instance.name = projectilePrefab.name + " (Pooled)";
            instance.gameObject.SetActive(false);
            pool.Add(instance);
            return instance;
        }

        private void RefreshTargetIfNeeded(bool force = false)
        {
            if (!force && targetRefreshRemaining > 0f && IsCurrentTargetRetainable())
            {
                return;
            }

            targetRefreshRemaining = shooterProfile.targetRefreshInterval;
            if (!IsCurrentTargetRetainable())
            {
                SetCurrentTarget(null);
            }

            if (currentTarget != null)
            {
                return;
            }

            Transform candidate = null;
            if (shooterProfile.targetingMode != ShooterTargetingMode.FindPlayer && assignedTarget != null)
            {
                candidate = assignedTarget;
            }

            if (candidate == null && shooterProfile.targetingMode != ShooterTargetingMode.AssignedOnly)
            {
                candidate = FindPlayerTarget();
            }

            if (IsCandidateAcquirable(candidate))
            {
                SetCurrentTarget(candidate);
            }
        }

        private Transform FindPlayerTarget()
        {
#if UNITY_2023_1_OR_NEWER
            PlayerMotor2D player = FindFirstObjectByType<PlayerMotor2D>();
#else
            PlayerMotor2D player = FindObjectOfType<PlayerMotor2D>();
#endif
            return player != null ? player.transform : null;
        }

        private bool IsCandidateAcquirable(Transform candidate)
        {
            if (!IsTargetAlive(candidate))
            {
                return false;
            }

            float distance = Vector2.Distance(GetAimOrigin(), GetTargetPoint(candidate));
            return distance <= shooterProfile.acquisitionRange;
        }

        private bool IsCurrentTargetRetainable()
        {
            if (!IsTargetAlive(currentTarget))
            {
                return false;
            }

            float distance = Vector2.Distance(GetAimOrigin(), GetTargetPoint(currentTarget));
            return distance <= shooterProfile.loseTargetRange;
        }

        private static bool IsTargetAlive(Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                return false;
            }

            PlayerVitality vitality = candidate.GetComponentInParent<PlayerVitality>();
            return vitality == null || !vitality.IsDead;
        }

        private void SetCurrentTarget(Transform target)
        {
            if (currentTarget == target)
            {
                return;
            }

            bool hadTarget = currentTarget != null;
            currentTarget = target;
            currentTargetBody = currentTarget != null ? currentTarget.GetComponentInParent<Rigidbody2D>() : null;
            wasInFireZone = false;
            oncePerEntryConsumed = false;

            if (hadTarget)
            {
                onTargetLost?.Invoke();
            }
            if (currentTarget != null)
            {
                AudioService.Instance?.Play(shooterProfile != null ? shooterProfile.targetAcquiredCue : null, transform.position);
                onTargetAcquired?.Invoke();
            }
        }

        private void UpdateAim(float delta)
        {
            if (aimPivot == null)
            {
                return;
            }

            if (currentTarget == null)
            {
                if (shooterProfile.returnToIdleWithoutTarget)
                {
                    Quaternion idle = Quaternion.Euler(0f, 0f, shooterProfile.idleLocalAngle);
                    aimPivot.localRotation = shooterProfile.turnSpeed <= 0f
                        ? idle
                        : Quaternion.RotateTowards(
                            aimPivot.localRotation,
                            idle,
                            shooterProfile.turnSpeed * delta);
                }
                return;
            }

            Vector2 origin = GetAimOrigin();
            Vector2 targetPoint = GetPredictedTargetPoint(origin);
            Vector2 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            desiredAimDirection = toTarget.normalized;
            float desiredWorldAngle = Mathf.Atan2(desiredAimDirection.y, desiredAimDirection.x) * Mathf.Rad2Deg -
                                      shooterProfile.visualForwardAngle;
            Quaternion desiredRotation = Quaternion.Euler(0f, 0f, desiredWorldAngle);
            aimPivot.rotation = shooterProfile.turnSpeed <= 0f
                ? desiredRotation
                : Quaternion.RotateTowards(
                    aimPivot.rotation,
                    desiredRotation,
                    shooterProfile.turnSpeed * delta);
        }

        private Vector2 GetPredictedTargetPoint(Vector2 origin)
        {
            Vector2 point = GetTargetPoint(currentTarget);
            if (!shooterProfile.predictTargetMovement || currentTargetBody == null ||
                projectileProfile == null || projectileProfile.speed <= 0.001f ||
                shooterProfile.maximumPredictionTime <= 0f)
            {
                return point;
            }

            Vector2 sourceVelocity = shooterBody != null
                ? shooterBody.linearVelocity * shooterProfile.inheritedShooterVelocity
                : Vector2.zero;
            Vector2 relativeVelocity = currentTargetBody.linearVelocity - sourceVelocity;
            Vector2 displacement = point - origin;
            if (!TrySolveInterceptTime(displacement, relativeVelocity, projectileProfile.speed, out float time))
            {
                return point;
            }

            time = Mathf.Clamp(time, 0f, shooterProfile.maximumPredictionTime);
            return point + currentTargetBody.linearVelocity * time;
        }

        private bool IsTargetInFireZone()
        {
            if (!IsTargetAlive(currentTarget))
            {
                return false;
            }

            Vector2 origin = GetAimOrigin();
            Vector2 targetPoint = GetTargetPoint(currentTarget);
            float distance = Vector2.Distance(origin, targetPoint);
            if (distance < shooterProfile.minimumFireRange || distance > shooterProfile.maximumFireRange)
            {
                return false;
            }

            return HasLineOfSight(origin, targetPoint);
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 targetPoint)
        {
            if (!shooterProfile.requireLineOfSight || shooterProfile.lineOfSightObstacleLayers.value == 0)
            {
                return true;
            }

            Vector2 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
            {
                return true;
            }

            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                direction / distance,
                distance,
                shooterProfile.lineOfSightObstacleLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (currentTarget != null &&
                    (collider.transform == currentTarget || collider.transform.IsChildOf(currentTarget)))
                {
                    return true;
                }
                return false;
            }

            return true;
        }

        private bool IsAimAligned()
        {
            if (desiredAimDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector2.Angle(GetCurrentAimDirection(), desiredAimDirection) <= shooterProfile.aimTolerance;
        }

        private Vector2 GetCurrentAimDirection()
        {
            Transform pivot = aimPivot != null ? aimPivot : transform;
            float angle = pivot.eulerAngles.z + shooterProfile.visualForwardAngle;
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private Vector2 GetAimOrigin()
        {
            if (muzzle != null) return muzzle.position;
            if (aimPivot != null) return aimPivot.position;
            return transform.position;
        }

        private Vector2 GetTargetPoint(Transform target)
        {
            if (target == null)
            {
                return GetAimOrigin() + desiredAimDirection;
            }

            return (Vector2)target.position + shooterProfile.targetOffset;
        }

        private bool TimeAllowsActions()
        {
            if (shooterProfile == null || !shooterProfile.pauseOutsideFlowingTime)
            {
                return true;
            }

            return TimeDirector.Instance == null || TimeDirector.Instance.Mode == TimeMode.Flowing;
        }

        private void ResolveMissingReferences()
        {
            if (aimPivot == null)
            {
                aimPivot = transform.Find("Aim Pivot");
            }
            if (muzzle == null && aimPivot != null)
            {
                muzzle = aimPivot.Find("Muzzle");
            }
            if (animator == null && aimPivot != null)
            {
                animator = aimPivot.GetComponentInChildren<Animator>();
            }
        }

        private void LogConfigurationWarningOnce()
        {
            if (configurationWarningLogged)
            {
                return;
            }

            configurationWarningLogged = true;
            Debug.LogWarning(
                "Configurable Shooter 2D is missing its projectile prefab or a required profile. Run the Time Echo combat asset builder or assign the missing reference.",
                this);
        }

        private static Vector2 Rotate(Vector2 direction, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private static bool TrySolveInterceptTime(
            Vector2 displacement,
            Vector2 relativeVelocity,
            float projectileSpeed,
            out float time)
        {
            float a = Vector2.Dot(relativeVelocity, relativeVelocity) - projectileSpeed * projectileSpeed;
            float b = 2f * Vector2.Dot(displacement, relativeVelocity);
            float c = Vector2.Dot(displacement, displacement);

            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) < 0.0001f)
                {
                    time = 0f;
                    return false;
                }

                time = -c / b;
                return time > 0f;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                time = 0f;
                return false;
            }

            float root = Mathf.Sqrt(discriminant);
            float first = (-b - root) / (2f * a);
            float second = (-b + root) / (2f * a);
            bool firstValid = first > 0f;
            bool secondValid = second > 0f;
            if (!firstValid && !secondValid)
            {
                time = 0f;
                return false;
            }

            time = firstValid && secondValid ? Mathf.Min(first, second) : (firstValid ? first : second);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (shooterProfile == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, shooterProfile.acquisitionRange);
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, shooterProfile.maximumFireRange);
            if (shooterProfile.minimumFireRange > 0f)
            {
                Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.7f);
                Gizmos.DrawWireSphere(transform.position, shooterProfile.minimumFireRange);
            }

            Vector2 origin = GetAimOrigin();
            Vector2 direction = Application.isPlaying && shooterProfile != null
                ? GetCurrentAimDirection()
                : (Vector2)(aimPivot != null ? aimPivot.right : transform.right);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + direction * 2f);
        }
    }
}
