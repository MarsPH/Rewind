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
            if (input == null || timeDirector == null || motor == null)
            {
                return;
            }

            InputFrame frame = input.Current;
            bool locked = PresentationDirector.Instance != null && PresentationDirector.Instance.InputLocked;
            if (locked || motor.IsDead)
            {
                CancelInteraction();
                return;
            }

            bool bothAimButtonsHeld = frame.SecondaryHeld && frame.PrimaryHeld;
            if (!bothAimButtonsHeld)
            {
                stasisBlockedUntilButtonRelease = false;
            }
            else if (temporalAimActive && timeDirector.Mode == TimeMode.Flowing)
            {
                stasisBlockedUntilButtonRelease = true;
            }

            if (frame.SecondaryReleased || !frame.SecondaryHeld)
            {
                suppressRewindUntilSecondaryRelease = false;
            }

            if (frame.PrimaryPressed)
            {
                BeginPrimaryCycle();
            }

            if (frame.PrimaryReleased && primaryCycleActive)
            {
                ExecuteCurrentAction();
                primaryCycleActive = false;
                temporalAimActive = false;
                arrow?.Hide();

                if (frame.SecondaryHeld)
                {
                    suppressRewindUntilSecondaryRelease = true;
                }

                timeDirector.SetMode(TimeMode.Flowing);
                return;
            }

            if (frame.SecondaryHeld && frame.PrimaryHeld)
            {
                if (stasisBlockedUntilButtonRelease)
                {
                    arrow?.Hide();
                    timeDirector.SetMode(TimeMode.Flowing);
                    return;
                }

                if (!primaryCycleActive)
                {
                    BeginPrimaryCycle();
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
            primaryPressedAt = Time.unscaledTime;
            currentCharge = GetTapCharge();
            UpdateDirection();
        }

        private void UpdateAim(bool inStasis)
        {
            UpdateDirection();
            float heldFor = Time.unscaledTime - primaryPressedAt;
            float threshold = tuning != null ? tuning.aim.holdThreshold : 0.14f;
            float chargeTime = tuning != null ? tuning.aim.fullChargeTime : 0.75f;
            float normalized = Mathf.Clamp01((heldFor - threshold) / Mathf.Max(0.01f, chargeTime));
            float curved = tuning != null && tuning.aim.chargeCurve != null
                ? tuning.aim.chargeCurve.Evaluate(normalized)
                : normalized;
            currentCharge = Mathf.Lerp(GetTapCharge(), 1f, Mathf.Clamp01(curved));

            if (inStasis || heldFor >= threshold)
            {
                arrow?.Show(transform.position, currentDirection, currentCharge, inStasis);
            }
            else
            {
                arrow?.Hide();
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

        private void ExecuteCurrentAction()
        {
            UpdateDirection();
            float charge = Mathf.Clamp01(currentCharge <= 0f ? GetTapCharge() : currentCharge);
            timeDirector.SetMode(TimeMode.Flowing);

            AimedActionMode mode = tuning != null ? tuning.aim.action : AimedActionMode.LaunchPlayer;
            if (mode == AimedActionMode.FireProjectile)
            {
                if (projectileLauncher == null)
                {
                    Debug.LogWarning("Aim action is FireProjectile, but no ProjectileLauncher is assigned.", this);
                    return;
                }

                float minimumSpeed = tuning != null ? tuning.aim.minimumProjectileSpeed : 10f;
                float maximumSpeed = tuning != null ? tuning.aim.maximumProjectileSpeed : 24f;
                projectileLauncher.TryFire(currentDirection, Mathf.Lerp(minimumSpeed, maximumSpeed, charge));
                return;
            }

            float minimumImpulse = tuning != null ? tuning.aim.minimumImpulse : 8f;
            float maximumImpulse = tuning != null ? tuning.aim.maximumImpulse : 18f;
            motor.TryLaunch(currentDirection, Mathf.Lerp(minimumImpulse, maximumImpulse, charge));
        }

        private float GetTapCharge()
        {
            return tuning != null ? tuning.aim.tapCharge : 0.55f;
        }

        private void CancelInteraction()
        {
            primaryCycleActive = false;
            temporalAimActive = false;
            suppressRewindUntilSecondaryRelease = false;
            stasisBlockedUntilButtonRelease = false;
            arrow?.Hide();
            timeDirector?.SetMode(TimeMode.Flowing);
        }
    }
}
