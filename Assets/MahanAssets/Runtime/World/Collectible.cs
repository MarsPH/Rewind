using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Collectible : MonoBehaviour, IRewindable
    {
        private struct Frame
        {
            public float Time;
            public bool Collected;
        }

        [SerializeField, Tooltip("Optional fixed ID. Leave empty on prefab assets to generate a unique ID from each scene instance.")]
        private string stableId;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Collider2D trigger;
        [SerializeField] private AudioCue pickupCue;
        [SerializeField, TextArea] private string pickupGuidance;

        private readonly List<Frame> frames = new List<Frame>(512);
        private bool collected;
        private string runtimeId;

        public bool IsCollected => collected;
        private string RuntimeId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(runtimeId))
                {
                    runtimeId = ResolveRuntimeId();
                }

                return runtimeId;
            }
        }

        private void Awake()
        {
            runtimeId = ResolveRuntimeId();
            if (trigger == null) trigger = GetComponent<Collider2D>();
            if (visualRoot == null) visualRoot = gameObject;
            trigger.isTrigger = true;
            ApplyState();
        }

        private void Start()
        {
            GameSession.Instance?.RegisterCollectible(RuntimeId);
        }

        private void OnEnable()
        {
            RewindRegistry.Register(this);
        }

        private void OnDisable()
        {
            RewindRegistry.Unregister(this);
        }

        private void OnValidate()
        {
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null) ownCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // A rewind restores the shard's visibility, but must never grant
            // energy merely because the player's collider overlaps it mid-rewind.
            if (collected || (TimeDirector.Instance != null && TimeDirector.Instance.Mode != TimeMode.Flowing))
            {
                return;
            }

            PlayerMotor2D player = other.GetComponentInParent<PlayerMotor2D>();
            if (player == null)
            {
                return;
            }

            PlayerVitality playerVitality = player.GetComponent<PlayerVitality>();
            // Read the shared tuning at pickup time so existing shard instances and prefab
            // variants use the current value without requiring individual prefab edits.
            GameTuning gameTuning = playerVitality != null ? playerVitality.Tuning : null;
            float energyRestoreFraction = gameTuning != null && gameTuning.collectible != null
                ? Mathf.Clamp01(gameTuning.collectible.energyRestoreFraction)
                : 0.25f;
            if (energyRestoreFraction > 0f && playerVitality == null)
            {
                Debug.LogWarning("This collectible restores energy, but the player has no PlayerVitality component.", this);
                return;
            }

            SetCollected(true);

            if (playerVitality != null && energyRestoreFraction > 0f)
            {
                // PlayerVitality.Heal clamps to Maximum and notifies the HUD.
                playerVitality.Heal(playerVitality.Maximum * energyRestoreFraction);
            }

            AudioService.Instance?.Play(pickupCue, transform.position);
            if (!string.IsNullOrWhiteSpace(pickupGuidance))
            {
                GuidanceDirector.Instance?.Show(pickupGuidance);
            }
        }

        public void Capture(float timelineTime)
        {
            Frame frame = new Frame { Time = timelineTime, Collected = collected };
            if (frames.Count > 0 && Mathf.Approximately(frames[frames.Count - 1].Time, timelineTime))
            {
                frames[frames.Count - 1] = frame;
            }
            else
            {
                frames.Add(frame);
            }

            float history = TimeDirector.Instance != null ? TimeDirector.Instance.HistorySeconds : 8f;
            float oldestAllowed = timelineTime - history;
            int removeCount = 0;
            while (removeCount < frames.Count - 1 && frames[removeCount].Time < oldestAllowed)
            {
                removeCount++;
            }

            if (removeCount > 0) frames.RemoveRange(0, removeCount);
        }

        public void Restore(float timelineTime)
        {
            // Collection is permanent.
            // Rewind must not restore collected shards.
            /*
            if (frames.Count == 0)
            {
                return;
            }

            int index = 0;
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].Time <= timelineTime + 0.0001f)
                {
                    index = i;
                    break;
                }
            }

            SetCollected(frames[index].Collected);
            */
        }

        public void TrimFuture(float timelineTime)
        {
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].Time > timelineTime + 0.0001f)
                {
                    frames.RemoveAt(i);
                }
            }

            Capture(timelineTime);
        }

        public void BeginRewind() { }
        public void EndRewind() { }

        public void Configure(string id, GameObject visuals, Collider2D targetTrigger)
        {
            stableId = id;
            runtimeId = null;
            visualRoot = visuals;
            trigger = targetTrigger;
        }

        private void SetCollected(bool value)
        {
            if (collected == value)
            {
                return;
            }

            collected = value;
            ApplyState();
            GameSession.Instance?.SetCollected(RuntimeId, collected);
        }

        private string ResolveRuntimeId()
        {
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                return stableId;
            }

            string hierarchyPath = $"{transform.GetSiblingIndex()}:{name}";
            Transform current = transform.parent;
            while (current != null)
            {
                hierarchyPath = $"{current.GetSiblingIndex()}:{current.name}/{hierarchyPath}";
                current = current.parent;
            }

            string sceneKey = gameObject.scene.path;
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                sceneKey = gameObject.scene.name;
            }

            return $"{sceneKey}/{hierarchyPath}#{GetInstanceID()}";
        }

        private void ApplyState()
        {
            if (visualRoot != null && visualRoot != gameObject)
            {
                visualRoot.SetActive(!collected);
            }
            else
            {
                Renderer renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = !collected;
            }

            if (trigger != null) trigger.enabled = !collected;
        }
    }
}
