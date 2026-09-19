using UnityEngine;

namespace TimeEcho
{
    public sealed class PlayerAudioFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerVitality vitality;
        [SerializeField] private TimeDirector timeDirector;

        [Header("Movement")]
        [SerializeField] private AudioCue runStep;
        [SerializeField] private AudioCue keyboardJump;
        [SerializeField] private AudioCue aimedLaunch;
        [SerializeField] private AudioCue land;
        [SerializeField] private AudioCue dashBoost;

        [Header("Energy and time")]
        [SerializeField] private AudioCue hurt;
        [SerializeField] private AudioCue death;
        [SerializeField] private AudioCue rewindStart;
        [SerializeField] private AudioCue rewindLoop;
        [SerializeField] private AudioCue rewindEnd;
        [SerializeField] private AudioCue tickingLoop;
        [SerializeField] private AudioCue stasisEnter;
        [SerializeField] private AudioCue stasisLoop;

        private AudioSource activeRewindLoop;
        private AudioSource activeTickingLoop;
        private AudioSource activeStasisLoop;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (vitality == null) vitality = GetComponent<PlayerVitality>();
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.Footstep += OnFootstep;
                motor.Jumped += OnJumped;
                motor.Launched += OnLaunched;
                motor.Landed += OnLanded;
            }

            if (vitality != null)
            {
                vitality.Damaged += OnDamaged;
                vitality.Died += OnDied;
            }

            if (timeDirector != null) timeDirector.ModeChanged += OnTimeModeChanged;
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Footstep -= OnFootstep;
                motor.Jumped -= OnJumped;
                motor.Launched -= OnLaunched;
                motor.Landed -= OnLanded;
            }

            if (vitality != null)
            {
                vitality.Damaged -= OnDamaged;
                vitality.Died -= OnDied;
            }

            if (timeDirector != null) timeDirector.ModeChanged -= OnTimeModeChanged;
            StopTemporalLoops();
        }

        public void Configure(PlayerMotor2D playerMotor, PlayerVitality playerVitality, TimeDirector director)
        {
            motor = playerMotor;
            vitality = playerVitality;
            timeDirector = director;
        }

        private void OnFootstep() => Play(runStep);
        private void OnJumped() => Play(keyboardJump);
        private void OnLaunched(Vector2 direction, float strength) => Play(aimedLaunch != null ? aimedLaunch : dashBoost);
        private void OnLanded(float speed) => Play(land);
        private void OnDamaged(float amount, Vector2 direction) => Play(hurt);
        private void OnDied() => Play(death);

        private void OnTimeModeChanged(TimeMode previous, TimeMode current)
        {
            AudioService service = AudioService.Instance;
            if (service == null)
            {
                return;
            }

            if (previous == TimeMode.Rewinding && current != TimeMode.Rewinding)
            {
                service.StopLoop(activeRewindLoop);
                service.StopLoop(activeTickingLoop);
                activeRewindLoop = null;
                activeTickingLoop = null;
                Play(rewindEnd);
            }

            if (previous == TimeMode.Stasis && current != TimeMode.Stasis)
            {
                service.StopLoop(activeStasisLoop);
                activeStasisLoop = null;
            }

            if (current == TimeMode.Rewinding)
            {
                Play(rewindStart);
                activeRewindLoop = service.PlayLoop(rewindLoop, transform);
                activeTickingLoop = service.PlayLoop(tickingLoop, transform);
            }
            else if (current == TimeMode.Stasis)
            {
                Play(stasisEnter);
                activeStasisLoop = service.PlayLoop(stasisLoop, transform);
            }
        }

        private void StopTemporalLoops()
        {
            AudioService service = AudioService.Instance;
            if (service == null) return;
            service.StopLoop(activeRewindLoop);
            service.StopLoop(activeTickingLoop);
            service.StopLoop(activeStasisLoop);
            activeRewindLoop = null;
            activeTickingLoop = null;
            activeStasisLoop = null;
        }

        private void Play(AudioCue cue)
        {
            AudioService.Instance?.Play(cue, transform);
        }
    }
}
