using System;
using UnityEngine;

namespace TimeEcho
{
    public enum TimeMode
    {
        Flowing,
        Rewinding,
        Stasis
    }

    [DefaultExecutionOrder(-800)]
    public sealed class TimeDirector : MonoBehaviour
    {
        public static TimeDirector Instance { get; private set; }

        [SerializeField] private GameTuning tuning;
        [SerializeField] private PlayerVitality vitality;

        private float timeline;
        private float oldestAvailableTime;
        private float stasisElapsed;
        private float timeScaleBeforeTemporalControl = 1f;
        private bool ownsTimeScale;

        public TimeMode Mode { get; private set; } = TimeMode.Flowing;
        public float Timeline => timeline;
        public float HistorySeconds => tuning != null ? tuning.rewind.historySeconds : 8f;
        public float AvailableHistory => Mathf.Max(0f, timeline - oldestAvailableTime);
        public float AvailableHistory01 => HistorySeconds <= 0f ? 0f : AvailableHistory / HistorySeconds;
        public bool CanRewind => timeline > oldestAvailableTime + 0.001f &&
                                 (vitality == null || tuning == null || vitality.Current >= tuning.rewind.minimumVitalityToStart);

        public event Action<TimeMode, TimeMode> ModeChanged;
        public event Action<float> TimelineChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one TimeDirector may exist in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            RewindRegistry.CaptureAll(timeline);
        }

        private void FixedUpdate()
        {
            if (Mode != TimeMode.Flowing)
            {
                return;
            }

            timeline += Time.fixedDeltaTime;
            oldestAvailableTime = Mathf.Max(oldestAvailableTime, timeline - HistorySeconds);
            RewindRegistry.CaptureAll(timeline);
            TimelineChanged?.Invoke(timeline);
        }

        private void Update()
        {
            float unscaledDelta = Time.unscaledDeltaTime;

            if (Mode == TimeMode.Rewinding)
            {
                if (!CanRewind)
                {
                    SetMode(TimeMode.Flowing);
                    return;
                }

                float speed = tuning != null ? tuning.rewind.playbackSpeed : 1f;
                timeline = Mathf.Max(oldestAvailableTime, timeline - unscaledDelta * speed);
                RewindRegistry.RestoreAll(timeline);
                Physics2D.SyncTransforms();
                SpendTemporalVitality(tuning != null ? tuning.rewind.vitalityCostPerSecond * unscaledDelta : 0f);
                TimelineChanged?.Invoke(timeline);

                if (timeline <= oldestAvailableTime + 0.0001f || (vitality != null && vitality.IsDead))
                {
                    SetMode(TimeMode.Flowing);
                }
            }
            else if (Mode == TimeMode.Stasis)
            {
                stasisElapsed += unscaledDelta;
                SpendTemporalVitality(tuning != null ? tuning.rewind.stasisVitalityCostPerSecond * unscaledDelta : 0f);

                float maximumStasis = tuning != null ? tuning.rewind.maximumStasisSeconds : 0f;
                if ((maximumStasis > 0f && stasisElapsed >= maximumStasis) ||
                    (vitality != null && vitality.IsDead))
                {
                    SetMode(TimeMode.Flowing);
                }
            }
        }

        private void OnDisable()
        {
            ReleaseRewindablesIfNeeded();
            ForceReleaseTimeScale();
        }

        private void OnDestroy()
        {
            ReleaseRewindablesIfNeeded();
            ForceReleaseTimeScale();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(GameTuning gameTuning, PlayerVitality playerVitality)
        {
            tuning = gameTuning;
            vitality = playerVitality;
        }

        public void SetMode(TimeMode next)
        {
            if (next == TimeMode.Rewinding && !CanRewind)
            {
                next = TimeMode.Flowing;
            }

            if (Mode == next)
            {
                return;
            }

            TimeMode previous = Mode;

            if (previous == TimeMode.Rewinding)
            {
                RewindRegistry.EndRewindAll();
                RewindRegistry.TrimFutureAll(timeline);
            }

            Mode = next;
            if (next == TimeMode.Rewinding)
            {
                RewindRegistry.CaptureAll(timeline);
                RewindRegistry.BeginRewindAll();
            }

            if (next == TimeMode.Stasis)
            {
                stasisElapsed = 0f;
            }

            ApplyTimeScale(next != TimeMode.Flowing);
            ModeChanged?.Invoke(previous, next);
        }

        private void SpendTemporalVitality(float amount)
        {
            if (vitality == null || amount <= 0f)
            {
                return;
            }

            bool canReachZero = tuning == null || tuning.vitality.rewindCanReduceToZero;
            vitality.SpendTemporal(amount, canReachZero);
        }

        private void ApplyTimeScale(bool frozen)
        {
            if (frozen)
            {
                if (!ownsTimeScale)
                {
                    timeScaleBeforeTemporalControl = Time.timeScale;
                    ownsTimeScale = true;
                }

                Time.timeScale = 0f;
                return;
            }

            ForceReleaseTimeScale();
        }

        private void ForceReleaseTimeScale()
        {
            if (!ownsTimeScale)
            {
                return;
            }

            Time.timeScale = timeScaleBeforeTemporalControl;
            ownsTimeScale = false;
        }

        private void ReleaseRewindablesIfNeeded()
        {
            if (Mode == TimeMode.Rewinding)
            {
                RewindRegistry.EndRewindAll();
            }

            Mode = TimeMode.Flowing;
        }
    }
}
