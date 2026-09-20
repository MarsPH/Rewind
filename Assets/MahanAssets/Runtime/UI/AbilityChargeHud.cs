using UnityEngine;
using UnityEngine.UI;

namespace TimeEcho
{
    // Uses the existing editable prefab slots. Shows only charges that can be used.
    // Pickups reveal more slots; using a boost hides one. No UI assets are instantiated.
    [DisallowMultipleComponent]
    public sealed class AbilityChargeHud : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        [Header("Sources (optional; auto-finds the player in this Gameplay Core)")]
        [SerializeField] private PlayerVitality vitality;
        [SerializeField] private GameTuning tuning;

        [Header("Prefab UI - both rows use the SAME charge count")]
        [SerializeField] private Image[] boostIcons = new Image[0];
        [SerializeField] private Image[] stasisIcons = new Image[0];
        [SerializeField] private Text boostCount;
        [SerializeField] private Text stasisCount;

        [Header("Optional numeric counters (off by default: icons only)")]
        [SerializeField] private bool showNumericCounts = false;

        private float lastCurrent = float.NaN;
        private float lastMaximum = float.NaN;
        private float lastCost = float.NaN;
        private bool lastCanReachZero;

        private void Awake()
        {
            ResolveSources();
        }

        private void OnEnable()
        {
            ResolveSources();
            if (vitality != null)
            {
                vitality.Changed -= OnVitalityChanged;
                vitality.Changed += OnVitalityChanged;
            }
            Refresh(true);
        }

        private void Start()
        {
            ResolveSources();
            Refresh(true);
        }

        private void OnDisable()
        {
            if (vitality != null) vitality.Changed -= OnVitalityChanged;
        }

        private void Update()
        {
            // Supports scene loads/late-spawned players and changes to GameTuning in Play Mode.
            if (vitality == null)
            {
                ResolveSources();
                if (vitality != null)
                {
                    vitality.Changed -= OnVitalityChanged;
                    vitality.Changed += OnVitalityChanged;
                }
            }
            Refresh(false);
        }

        private void ResolveSources()
        {
            if (vitality == null)
            {
                // Prefer the player belonging to this HUD, not an unrelated player in another root.
                Transform core = transform.root;
                vitality = core.GetComponentInChildren<PlayerVitality>(true);
                if (vitality == null) vitality = FindObjectOfType<PlayerVitality>();
            }
            if (tuning == null && vitality != null) tuning = vitality.Tuning;
        }

        private void OnVitalityChanged(float current, float maximum)
        {
            Refresh(true);
        }

        private void Refresh(bool force)
        {
            if (vitality == null) return;
            if (tuning == null) tuning = vitality.Tuning;

            float maximum = vitality.Maximum;
            float current = vitality.Current;
            float fraction = tuning != null ? Mathf.Clamp01(tuning.aim.boostVitalityCostFraction) : 0.25f;
            float cost = maximum * fraction;
            bool canReachZero = tuning == null || tuning.vitality.boostCanReduceToZero;

            if (!force && Mathf.Approximately(lastCurrent, current) &&
                Mathf.Approximately(lastMaximum, maximum) && Mathf.Approximately(lastCost, cost) &&
                lastCanReachZero == canReachZero)
                return;

            lastCurrent = current;
            lastMaximum = maximum;
            lastCost = cost;
            lastCanReachZero = canReachZero;

            bool unlimited = cost <= Epsilon;
            float reserve = canReachZero ? 0f : Mathf.Min(1f, maximum);
            int total = unlimited ? int.MaxValue : Mathf.Max(0, Mathf.FloorToInt((maximum - reserve + Epsilon) / cost));
            int available = unlimited ? int.MaxValue :
                (vitality.IsDead ? 0 : Mathf.Clamp(Mathf.FloorToInt((current - reserve + Epsilon) / cost), 0, total));

            Paint(boostIcons, available);
            Paint(stasisIcons, available);

            string count = unlimited ? "∞" : available + "/" + total;
            UpdateCounter(boostCount, count);
            UpdateCounter(stasisCount, count);
        }

        private void Paint(Image[] icons, int available)
        {
            if (icons == null) return;
            for (int i = 0; i < icons.Length; i++)
            {
                Image image = icons[i];
                if (image == null) continue;

                // Generated prefab: Charge N (background + icon) -> Icon.
                // Hide the whole slot, not just the icon, to avoid empty plates.
                // If the user rearranged the hierarchy, hide only the Image.
                Transform parent = image.transform.parent;
                GameObject slot = parent != null && parent.name.StartsWith("Charge ")
                    ? parent.gameObject : image.gameObject;
                bool visible = i < available;
                if (slot.activeSelf != visible)
                    slot.SetActive(visible);
            }
        }

        private void UpdateCounter(Text counter, string text)
        {
            if (counter == null) return;
            if (counter.gameObject.activeSelf != showNumericCounts)
                counter.gameObject.SetActive(showNumericCounts);
            if (showNumericCounts) counter.text = text;
        }
    }
}
