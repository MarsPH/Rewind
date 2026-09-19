using UnityEngine;
using UnityEngine.UI;

namespace TimeEcho
{
    public sealed class GameHud : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private GameTuning tuning;
        [SerializeField] private PlayerVitality vitality;
        [SerializeField] private TimeDirector timeDirector;
        [SerializeField] private RunTimer runTimer;
        [SerializeField] private GameSession session;

        [Header("Views")]
        [SerializeField] private Image vitalityFill;
        [SerializeField] private Image historyFill;
        [SerializeField] private Image temporalOverlay;
        [SerializeField] private Text vitalityText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text collectibleText;
        [SerializeField] private Text modeText;

        private void Awake()
        {
            if (vitality == null) vitality = FindObjectOfType<PlayerVitality>();
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();
            if (runTimer == null) runTimer = FindObjectOfType<RunTimer>();
            if (session == null) session = FindObjectOfType<GameSession>();
        }

        private void OnEnable()
        {
            if (vitality != null) vitality.Changed += OnVitalityChanged;
            if (timeDirector != null)
            {
                timeDirector.ModeChanged += OnModeChanged;
                timeDirector.TimelineChanged += OnTimelineChanged;
            }
            if (runTimer != null) runTimer.Changed += OnTimerChanged;
            if (session != null) session.CollectibleCountChanged += OnCollectiblesChanged;
        }

        private void Start()
        {
            if (vitality != null) OnVitalityChanged(vitality.Current, vitality.Maximum);
            if (timeDirector != null)
            {
                OnModeChanged(timeDirector.Mode, timeDirector.Mode);
                OnTimelineChanged(timeDirector.Timeline);
            }
            if (runTimer != null) OnTimerChanged(runTimer.Elapsed);
            if (session != null) OnCollectiblesChanged(session.CollectedCount, session.TotalCollectibles);
        }

        private void OnDisable()
        {
            if (vitality != null) vitality.Changed -= OnVitalityChanged;
            if (timeDirector != null)
            {
                timeDirector.ModeChanged -= OnModeChanged;
                timeDirector.TimelineChanged -= OnTimelineChanged;
            }
            if (runTimer != null) runTimer.Changed -= OnTimerChanged;
            if (session != null) session.CollectibleCountChanged -= OnCollectiblesChanged;
        }

        public void Configure(
            GameTuning gameTuning,
            PlayerVitality playerVitality,
            TimeDirector director,
            RunTimer timer,
            GameSession gameSession,
            Image vitalityBar,
            Image rewindHistoryBar,
            Image overlay,
            Text vitalityLabel,
            Text timerLabel,
            Text collectibleLabel,
            Text modeLabel)
        {
            tuning = gameTuning;
            vitality = playerVitality;
            timeDirector = director;
            runTimer = timer;
            session = gameSession;
            vitalityFill = vitalityBar;
            historyFill = rewindHistoryBar;
            temporalOverlay = overlay;
            vitalityText = vitalityLabel;
            timerText = timerLabel;
            collectibleText = collectibleLabel;
            modeText = modeLabel;
        }

        private void OnVitalityChanged(float current, float maximum)
        {
            if (vitalityFill != null) vitalityFill.fillAmount = maximum <= 0f ? 0f : current / maximum;
            if (vitalityText != null) vitalityText.text = $"LIFE  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
            if (modeText != null)
            {
                if (current <= 0f)
                {
                    modeText.text = "OUT OF TIME";
                }
                else if (timeDirector != null)
                {
                    OnModeChanged(timeDirector.Mode, timeDirector.Mode);
                }
            }
        }

        private void OnTimelineChanged(float value)
        {
            if (historyFill != null && timeDirector != null)
            {
                historyFill.fillAmount = timeDirector.AvailableHistory01;
            }
        }

        private void OnTimerChanged(float seconds)
        {
            if (timerText == null)
            {
                return;
            }

            int totalHundredths = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f));
            int minutes = totalHundredths / 6000;
            int wholeSeconds = totalHundredths / 100 % 60;
            int hundredths = totalHundredths % 100;
            timerText.text = $"{minutes:00}:{wholeSeconds:00}.{hundredths:00}";
        }

        private void OnCollectiblesChanged(int collected, int total)
        {
            if (collectibleText != null) collectibleText.text = $"SHARDS  {collected} / {total}";
        }

        private void OnModeChanged(TimeMode previous, TimeMode current)
        {
            if (modeText != null)
            {
                modeText.text = vitality != null && vitality.IsDead
                    ? "OUT OF TIME"
                    : current == TimeMode.Flowing
                        ? string.Empty
                        : current.ToString().ToUpperInvariant();
            }

            if (temporalOverlay == null)
            {
                return;
            }

            Color color;
            if (current == TimeMode.Rewinding)
            {
                float alpha = tuning != null ? tuning.feedback.rewindOverlayAlpha : 0.16f;
                color = new Color(0.15f, 0.65f, 1f, alpha);
            }
            else if (current == TimeMode.Stasis)
            {
                float alpha = tuning != null ? tuning.feedback.stasisOverlayAlpha : 0.12f;
                color = new Color(1f, 0.72f, 0.2f, alpha);
            }
            else
            {
                color = Color.clear;
            }

            temporalOverlay.color = color;
            temporalOverlay.enabled = current != TimeMode.Flowing;
        }
    }
}
