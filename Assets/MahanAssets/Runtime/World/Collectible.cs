using System;
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

        [SerializeField] private string stableId;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Collider2D trigger;
        [SerializeField] private AudioCue pickupCue;
        [SerializeField, TextArea] private string pickupGuidance;

        private readonly List<Frame> frames = new List<Frame>(512);
        private bool collected;

        public bool IsCollected => collected;

        private void Awake()
        {
            if (trigger == null) trigger = GetComponent<Collider2D>();
            if (visualRoot == null) visualRoot = gameObject;
            trigger.isTrigger = true;
            ApplyState();
        }

        private void Start()
        {
            GameSession.Instance?.RegisterCollectible(stableId);
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
            if (string.IsNullOrWhiteSpace(stableId))
            {
                stableId = Guid.NewGuid().ToString("N");
            }

            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null) ownCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other.GetComponentInParent<PlayerMotor2D>() == null)
            {
                return;
            }

            SetCollected(true);
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
            GameSession.Instance?.SetCollected(stableId, collected);
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
