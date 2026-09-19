using UnityEngine;

namespace TimeEcho
{
    [DefaultExecutionOrder(-900)]
    public sealed class AimedActionController : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameInput input;
        [SerializeField] private TimeDirector timeDirector;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerVitality vitality;
        [SerializeField] private AimArrowView arrow;
        [SerializeField] private ProjectileLauncher projectileLauncher;
        [SerializeField] private Camera worldCamera;

        private bool primaryCycleActive;
        private bool aimPreviewShown;
        private bool aimReady;
        private bool temporalAimActive;
        private bool stasisBlockedUntilButtonRelease;
        private bool suppressRewindUntilSecondaryRelease;
        private float primaryPressedAt;
        private Vector2 currentDirection = Vector2.right;
        private float currentCharge;

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<GameInput>();
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (vitality == null) vitality = GetComponent<PlayerVitality>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (timeDirector != null)
            {
                timeDirector.ModeChanged += OnTimeModeChanged;
            }
        }

        private void Update()
        {
            if (timeDirector == null || motor == null)
            {
                return;
            }

            if (input == null)
            {
                CancelInteraction();
                return;
            }

            InputFrame frame = input.Current;

            // Rewind is hold-only. Release it before processing any other mouse action
            // so time, audio, and the overlay all return to Flowing in this frame.
            if (!frame.SecondaryHeld && timeDirector.Mode == TimeMode.Rewinding)
            {
                timeDirector.SetMode(TimeMode.Flowing);
            }

            bool locked = PresentationDirector.Instance != null && PresentationDirector.Instance.InputLocked;
            if (locked || motor.IsDead)
            {
                CancelInteraction();
                return;
            }

            // A Stasis action is latched when the two-button aim begins. Resolve
            // its first release BEFORE ordinary aim/rewind cleanup, even if a
            // TimeDirector timeout changed the mode between input frames.
            // Never depend on Mode still being Stasis to deliver the launch.
            if (temporalAimActive &&
                (frame.PrimaryReleased || frame.SecondaryReleased ||
                 !frame.PrimaryHeld || !frame.SecondaryHeld))
            {
                ExecuteCurrentAction(true);
                FinishAimCycle(frame.SecondaryHeld);
                return;
            }

            bool bothAimButtonsHeld = frame.SecondaryHeld && frame.PrimaryHeld;
            if (!bothAimButtonsHeld)
            {
                stasisBlockedUntilButtonRelease = false;
            }

            if (frame.SecondaryReleased || !frame.SecondaryHeld)
            {
                suppressRewindUntilSecondaryRelease = false;
            }

            if (frame.PrimaryPressed && !temporalAimActive)
            {
                BeginPrimaryCycle();
            }

            // If a configured Stasis duration ends before release, keep the
            // already-aimed action pending rather than discarding the boost.
            // Do not re-enter Stasis and restart its duration every frame.
            if (temporalAimActive && bothAimButtonsHeld &&
                timeDirector.Mode != TimeMode.Stasis)
            {
                return;
            }

            if (frame.PrimaryReleased && primaryCycleActive)
            {
                // Quick clicks still cancel; only an armed arrow can launch.
                if (aimReady)
                {
                    ExecuteCurrentAction(false);
                }

                FinishAimCycle(frame.SecondaryHeld);
                return;
            }

            if (frame.SecondaryHeld && frame.PrimaryHeld)
            {
                if (stasisBlockedUntilButtonRelease)
                {
                    // Stasis timed out while both buttons were held. Do not
                    // allow a later release to fire an invisible, stale aim.
                    primaryCycleActive = false;
                    aimPreviewShown = false;
                    aimReady = false;
                    suppressRewindUntilSecondaryRelease = true;
                    arrow?.Hide();
                    timeDirector.SetMode(TimeMode.Flowing);
                    return;
                }

                if (!primaryCycleActive)
                {
                    // Only an actual LMB press may begin a new aiming cycle.
                    arrow?.Hide();
                    timeDirector.SetMode(TimeMode.Flowing);
                    return;
                }

                temporalAimActive = true;
                timeDirector.SetMode(TimeMode.Stasis);
                UpdateAim(true);
                return;
            }

            if (frame.SecondaryHeld)
            {
                arrow?.Hide();
                temporalAimActive = false;
                bool canRewind = timeDirector.CanRewind;
                if (!canRewind)
                {
                    suppressRewindUntilSecondaryRelease = true;
                }

                if (!suppressRewindUntilSecondaryRelease && canRewind)
                {
                    timeDirector.SetMode(TimeMode.Rewinding);
                }
                else
                {
                    timeDirector.SetMode(TimeMode.Flowing);
                }

                return;
            }

            timeDirector.SetMode(TimeMode.Flowing);
            temporalAimActive = false;

            if (frame.PrimaryHeld && primaryCycleActive)
            {
                UpdateAim(false);
            }
            else
            {
                arrow?.Hide();
            }
        }

        private void OnDisable()
        {
            if (timeDirector != null)
            {
                timeDirector.ModeChanged -= OnTimeModeChanged;
            }

            arrow?.Hide();
            if (timeDirector != null)
            {
                timeDirector.SetMode(TimeMode.Flowing);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelInteraction();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                CancelInteraction();
            }
        }

        public void Configure(
            GameTuning gameTuning,
            GameInput gameInput,
            TimeDirector director,
            PlayerMotor2D playerMotor,
            PlayerVitality playerVitality,
            AimArrowView arrowView,
            Camera camera,
            ProjectileLauncher launcher = null)
        {
            tuning = gameTuning;
            input = gameInput;
            timeDirector = director;
            motor = playerMotor;
            vitality = playerVitality;
            arrow = arrowView;
            worldCamera = camera;
            projectileLauncher = launcher;
        }

        private void OnTimeModeChanged(TimeMode previous, TimeMode current)
        {
            if (previous != TimeMode.Rewinding || current != TimeMode.Flowing || input == null)
            {
                return;
            }

            if (input.Current.SecondaryHeld)
            {
                suppressRewindUntilSecondaryRelease = true;
            }
        }

        private void BeginPrimaryCycle()
        {
            primaryCycleActive = true;
            aimPreviewShown = false;
            aimReady = false;
            primaryPressedAt = Time.unscaledTime;
            currentCharge = GetTapCharge();
            UpdateDirection();
        }

        private void UpdateAim(bool inStasis)
        {
            UpdateDirection();
            float heldFor = Time.unscaledTime - primaryPressedAt;
            float threshold = tuning != null ? Mathf.Max(0.1f, tuning.aim.holdThreshold) : 0.18f;
            float chargeTime = tuning != null ? tuning.aim.fullChargeTime : 0.75f;
            float normalized = Mathf.Clamp01((heldFor - threshold) / Mathf.Max(0.01f, chargeTime));
            float curved = tuning != null && tuning.aim.chargeCurve != null
                ? tuning.aim.chargeCurve.Evaluate(normalized)
                : normalized;
            currentCharge = Mathf.Lerp(GetTapCharge(), 1f, Mathf.Clamp01(curved));

            // Two-button stasis is already an intentional aim: the arrow is
            // ready as soon as it appears. Ordinary LMB still needs the hold
            // threshold to avoid accidental boosts from a quick click.
            bool heldLongEnough = inStasis || heldFor >= threshold;
            if (arrow != null)
            {
                arrow.Show(transform.position, currentDirection, currentCharge, inStasis, heldLongEnough);
                aimPreviewShown = true;
            }

            // The arrow is feedback, not a gameplay dependency. Stasis is
            // intentionally ready on entry even if its renderer is not assigned.
            if (inStasis || (aimPreviewShown && heldLongEnough))
            {
                aimReady = true;
            }
        }

        private void UpdateDirection()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                currentDirection = new Vector2(motor.FacingSign, 0f);
                return;
            }

            Vector3 pointer = worldCamera.ScreenToWorldPoint(input.Current.PointerScreen);
            Vector2 delta = (Vector2)pointer - (Vector2)transform.position;
            float deadZone = tuning != null ? tuning.aim.pointerDeadZone : 0.1f;
            currentDirection = delta.sqrMagnitude >= deadZone * deadZone
                ? delta.normalized
                : new Vector2(motor.FacingSign, 0f);
        }

        private void ExecuteCurrentAction(bool fromStasis)
        {
            // Stasis commits the exact vector that the arrow last displayed.
            // Do not recalculate it on the release frame or after time resumes.
            if (!fromStasis)
            {
                UpdateDirection();
            }

            Vector2 launchDirection = currentDirection;
            float charge = Mathf.Clamp01(currentCharge <= 0f ? GetTapCharge() : currentCharge);
            timeDirector.SetMode(TimeMode.Flowing);

            AimedActionMode mode = tuning != null ? tuning.aim.action : AimedActionMode.LaunchPlayer;
            // Stasis always boosts the player as requested, independently of
            // the normal-action projectile setting.
            if (!fromStasis && mode == AimedActionMode.FireProjectile)
            {
                if (projectileLauncher == null)
                {
                    Debug.LogWarning("Aim action is FireProjectile, but no ProjectileLauncher is assigned.", this);
                    return;
                }

                float minimumSpeed = tuning != null ? tuning.aim.minimumProjectileSpeed : 10f;
                float maximumSpeed = tuning != null ? tuning.aim.maximumProjectileSpeed : 24f;
                projectileLauncher.TryFire(launchDirection, Mathf.Lerp(minimumSpeed, maximumSpeed, charge));
                return;
            }

            float minimumImpulse = tuning != null ? tuning.aim.minimumImpulse : 8f;
            float maximumImpulse = tuning != null ? tuning.aim.maximumImpulse : 18f;
            if (fromStasis && vitality == null)
            {
                Debug.LogWarning("Stasis boost blocked: PlayerVitality is missing. Assign or add it so the normal boost cost can be paid.", this);
                return;
            }

            float boostCost = GetBoostVitalityCost();
            bool canReachZero = tuning == null || tuning.vitality.boostCanReduceToZero;
            if (vitality != null && !vitality.CanSpendTemporal(boostCost, canReachZero))
            {
                if (fromStasis)
                {
                    Debug.LogWarning($"Stasis boost blocked: insufficient energy ({vitality.Current:0.##} available, {boostCost:0.##} needed).", this);
                }
                return;
            }

            // Stasis is a deliberate two-button action and must not silently fail
            // during the ordinary boost's short cooldown. Normal boosts still
            // respect the cooldown, and both actions charge the same amount.
            bool launched = motor.TryLaunch(
                launchDirection, Mathf.Lerp(minimumImpulse, maximumImpulse, charge),
                fromStasis, fromStasis);
            if (launched)
            {
                vitality?.TrySpendTemporal(boostCost, canReachZero);
            }
            else if (fromStasis)
            {
                Debug.LogWarning("Stasis boost blocked by PlayerMotor2D: check that the player is alive, has a Rigidbody2D, and the arrow direction is valid.", this);
            }
        }

        private float GetBoostVitalityCost()
        {
            if (vitality == null)
            {
                return 0f;
            }

            float fraction = tuning != null ? tuning.aim.boostVitalityCostFraction : 0.25f;
            return vitality.Maximum * Mathf.Clamp01(fraction);
        }

        private float GetTapCharge()
        {
            return tuning != null ? tuning.aim.tapCharge : 0.55f;
        }

        private void FinishAimCycle(bool secondaryStillHeld)
        {
            primaryCycleActive = false;
            aimPreviewShown = false;
            aimReady = false;
            temporalAimActive = false;
            if (secondaryStillHeld)
            {
                // Releasing LMB first must not immediately rewind the boost.
                // Ignore the held RMB until the player releases and represses it.
                suppressRewindUntilSecondaryRelease = true;
            }

            arrow?.Hide();
            timeDirector.SetMode(TimeMode.Flowing);
        }

        private void CancelInteraction()
        {
            primaryCycleActive = false;
            aimPreviewShown = false;
            aimReady = false;
            temporalAimActive = false;
            suppressRewindUntilSecondaryRelease = false;
            stasisBlockedUntilButtonRelease = false;
            arrow?.Hide();
            timeDirector?.SetMode(TimeMode.Flowing);
        }
    }
}
