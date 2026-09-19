using UnityEngine;

namespace TimeEcho
{
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerVitality vitality;
        [SerializeField] private TimeDirector timeDirector;

        [Header("Animator parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string verticalSpeedParameter = "VerticalSpeed";
        [SerializeField] private string groundedParameter = "Grounded";
        [SerializeField] private string deadParameter = "Dead";
        [SerializeField] private string rewindingParameter = "Rewinding";
        [SerializeField] private string launchTrigger = "Launch";

        private int speedHash;
        private int verticalSpeedHash;
        private int groundedHash;
        private int deadHash;
        private int rewindingHash;
        private int launchHash;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (vitality == null) vitality = GetComponent<PlayerVitality>();
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();

            speedHash = Animator.StringToHash(speedParameter);
            verticalSpeedHash = Animator.StringToHash(verticalSpeedParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
            deadHash = Animator.StringToHash(deadParameter);
            rewindingHash = Animator.StringToHash(rewindingParameter);
            launchHash = Animator.StringToHash(launchTrigger);
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.FacingChanged += OnFacingChanged;
                motor.Launched += OnLaunched;
            }

        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.FacingChanged -= OnFacingChanged;
                motor.Launched -= OnLaunched;
            }

        }

        private void Update()
        {
            if (animator == null || motor == null)
            {
                return;
            }

            Vector2 velocity = motor.Velocity;
            animator.SetFloat(speedHash, Mathf.Abs(velocity.x));
            animator.SetFloat(verticalSpeedHash, velocity.y);
            animator.SetBool(groundedHash, motor.IsGrounded);
            animator.SetBool(deadHash, vitality != null && vitality.IsDead);
            animator.SetBool(rewindingHash, timeDirector != null && timeDirector.Mode == TimeMode.Rewinding);

            animator.updateMode = timeDirector != null && timeDirector.Mode != TimeMode.Flowing
                ? AnimatorUpdateMode.UnscaledTime
                : AnimatorUpdateMode.Normal;
            animator.speed = timeDirector != null && timeDirector.Mode == TimeMode.Stasis ? 0f : 1f;
        }

        public void Configure(Animator targetAnimator, SpriteRenderer targetRenderer, PlayerMotor2D playerMotor, PlayerVitality playerVitality, TimeDirector director)
        {
            animator = targetAnimator;
            spriteRenderer = targetRenderer;
            motor = playerMotor;
            vitality = playerVitality;
            timeDirector = director;
        }

        private void OnFacingChanged(float sign)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = sign < 0f;
            }
        }

        private void OnLaunched(Vector2 direction, float impulse)
        {
            if (animator != null)
            {
                animator.SetTrigger(launchHash);
            }
        }

    }
}
