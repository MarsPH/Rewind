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
        [SerializeField, HideInInspector] private Image historyFill;
        [SerializeField] private Image temporalOverlay;
        [SerializeField] private Text vitalityText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text collectibleText;
        [SerializeField] private Text modeText;

        private TimeMode presentedMode;
        private bool hasPresentedMode;

        private void Awake()
        {
            if (vitality == null) vitality = FindObjectOfType<PlayerVitality>();
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();
            if (runTimer == null) runTimer = FindObjectOfType<RunTimer>();
            if (session == null) session = FindObjectOfType<GameSession>();
            HideLegacyHistoryBar();
            // Editable HUD lives as a serialized prefab child. Never create HUD objects at runtime.
            // Existing projects without the prefab retain their original bar until installed.
            if (GetComponentInChildren<AbilityChargeHud>(true) != null)
            {
                HideLegacyVitalityBar();
            }
        }

        private void OnEnable()
        {
            if (vitality != null) vitality.Changed += OnVitalityChanged;
            if (timeDirector != null)
            {
                timeDirector.ModeChanged += OnModeChanged;
            }
            if (runTimer != null) runTimer.Changed += OnTimerChanged;
            if (session != null) session.CollectibleCountChanged += OnCollectiblesChanged;

            HideLegacyHistoryBar();
            hasPresentedMode = false;
            if (timeDirector != null)
            {
                OnModeChanged(timeDirector.Mode, timeDirector.Mode);
            }
        }

        private void Start()
        {
            if (vitality != null) OnVitalityChanged(vitality.Current, vitality.Maximum);
            if (timeDirector != null)
            {
                OnModeChanged(timeDirector.Mode, timeDirector.Mode);
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
            }
            if (runTimer != null) runTimer.Changed -= OnTimerChanged;
            if (session != null) session.CollectibleCountChanged -= OnCollectiblesChanged;
            hasPresentedMode = false;
        }

        public void Configure(
            GameTuning gameTuning,
            PlayerVitality playerVitality,
            TimeDirector director,
            RunTimer timer,
            GameSession gameSession,
            Image vitalityBar,
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
            historyFill = null;
            temporalOverlay = overlay;
            vitalityText = vitalityLabel;
            timerText = timerLabel;
            collectibleText = collectibleLabel;
            modeText = modeLabel;
        }

        // Keeps projects with code that called the pre-1.0.7 setup API compiling.
        // The legacy history bar is accepted only so it can be hidden.
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
            Configure(
                gameTuning,
                playerVitality,
                director,
                timer,
                gameSession,
                vitalityBar,
                overlay,
                vitalityLabel,
                timerLabel,
                collectibleLabel,
                modeLabel);
            historyFill = rewindHistoryBar;
            HideLegacyHistoryBar();
        }

        private void LateUpdate()
        {
            if (timeDirector == null)
            {
                return;
            }

            bool overlayShouldBeVisible = timeDirector.Mode != TimeMode.Flowing;
            if (!hasPresentedMode || presentedMode != timeDirector.Mode ||
                (temporalOverlay != null && temporalOverlay.enabled != overlayShouldBeVisible))
            {
                OnModeChanged(timeDirector.Mode, timeDirector.Mode);
            }
        }

        private void OnVitalityChanged(float current, float maximum)
        {
            if (vitalityFill != null) vitalityFill.fillAmount = maximum <= 0f ? 0f : current / maximum;
            if (vitalityText != null) vitalityText.text = $"ENERGY  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
            if (modeText != null)
            {
                if (vitality != null && vitality.IsDead)
                {
                    modeText.text = "OUT OF TIME";
                }
                else if (timeDirector != null)
                {
                    OnModeChanged(timeDirector.Mode, timeDirector.Mode);
                }
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
            presentedMode = current;
            hasPresentedMode = true;

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

        private void HideLegacyVitalityBar()
        {
            if (vitalityFill != null)
            {
                Transform barParent = vitalityFill.transform.parent;
                // Never disable a Canvas or the GameHud root by mistake.
                if (barParent != null && barParent != transform &&
                    barParent.GetComponent<Canvas>() == null)
                {
                    barParent.gameObject.SetActive(false);
                }
                else
                {
                    vitalityFill.gameObject.SetActive(false);
                }
            }

            if (vitalityText != null)
            {
                vitalityText.gameObject.SetActive(false);
            }
        }

        private void HideLegacyHistoryBar()
        {
            if (historyFill == null)
            {
                return;
            }

            Transform barRoot = historyFill.transform.parent;
            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(false);
            }
            else
            {
                historyFill.gameObject.SetActive(false);
            }
        }
    }
}
