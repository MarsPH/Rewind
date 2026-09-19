using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RewindableRigidbody2D : MonoBehaviour, IRewindable
    {
        private struct Frame
        {
            public float Time;
            public Vector2 Position;
            public float Rotation;
            public Vector2 Velocity;
            public float AngularVelocity;
            public float GravityScale;
            public bool Simulated;
        }

        [SerializeField] private float historySecondsOverride;

        private readonly List<Frame> frames = new List<Frame>(512);
        private Rigidbody2D body;
        private Frame restoredFrame;
        private bool hasRestoredFrame;
        private bool simulatedBeforeRewind;
        private RigidbodyInterpolation2D interpolationBeforeRewind;

        private float HistorySeconds
        {
            get
            {
                if (historySecondsOverride > 0f)
                {
                    return historySecondsOverride;
                }

                return TimeDirector.Instance != null ? TimeDirector.Instance.HistorySeconds : 8f;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            RewindRegistry.Register(this);
        }

        private void OnDisable()
        {
            RewindRegistry.Unregister(this);
        }

        public void Capture(float timelineTime)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            Frame frame = new Frame
            {
                Time = timelineTime,
                Position = body.position,
                Rotation = body.rotation,
                Velocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity,
                GravityScale = body.gravityScale,
                Simulated = body.simulated
            };

            if (frames.Count > 0 && Mathf.Approximately(frames[frames.Count - 1].Time, timelineTime))
            {
                frames[frames.Count - 1] = frame;
            }
            else
            {
                frames.Add(frame);
            }

            float oldestAllowed = timelineTime - HistorySeconds;
            int removeCount = 0;
            while (removeCount < frames.Count - 1 && frames[removeCount].Time < oldestAllowed)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                frames.RemoveRange(0, removeCount);
            }
        }

        public void Restore(float timelineTime)
        {
            if (frames.Count == 0 || body == null)
            {
                return;
            }

            int rightIndex = FindFirstFrameAtOrAfter(timelineTime);
            int leftIndex = Mathf.Max(0, rightIndex - 1);
            rightIndex = Mathf.Clamp(rightIndex, 0, frames.Count - 1);

            Frame left = frames[leftIndex];
            Frame right = frames[rightIndex];
            float range = right.Time - left.Time;
            float t = range <= Mathf.Epsilon ? 0f : Mathf.Clamp01((timelineTime - left.Time) / range);

            restoredFrame = new Frame
            {
                Time = timelineTime,
                Position = Vector2.Lerp(left.Position, right.Position, t),
                Rotation = Mathf.LerpAngle(left.Rotation, right.Rotation, t),
                Velocity = Vector2.Lerp(left.Velocity, right.Velocity, t),
                AngularVelocity = Mathf.Lerp(left.AngularVelocity, right.AngularVelocity, t),
                GravityScale = Mathf.Lerp(left.GravityScale, right.GravityScale, t),
                Simulated = t < 0.5f ? left.Simulated : right.Simulated
            };

            body.position = restoredFrame.Position;
            body.rotation = restoredFrame.Rotation;
            transform.SetPositionAndRotation(
                new Vector3(restoredFrame.Position.x, restoredFrame.Position.y, transform.position.z),
                Quaternion.Euler(0f, 0f, restoredFrame.Rotation));
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            hasRestoredFrame = true;
        }

        public void TrimFuture(float timelineTime)
        {
            int firstFuture = FindFirstFrameAfter(timelineTime);
            if (firstFuture >= 0 && firstFuture < frames.Count)
            {
                frames.RemoveRange(firstFuture, frames.Count - firstFuture);
            }

            Capture(timelineTime);
        }

        public void BeginRewind()
        {
            if (body == null)
            {
                return;
            }

            simulatedBeforeRewind = body.simulated;
            interpolationBeforeRewind = body.interpolation;
            body.interpolation = RigidbodyInterpolation2D.None;
            hasRestoredFrame = false;
        }

        public void EndRewind()
        {
            if (body == null)
            {
                return;
            }

            body.simulated = hasRestoredFrame ? restoredFrame.Simulated : simulatedBeforeRewind;
            if (hasRestoredFrame)
            {
                body.gravityScale = restoredFrame.GravityScale;
                body.linearVelocity = restoredFrame.Velocity;
                body.angularVelocity = restoredFrame.AngularVelocity;
            }

            body.interpolation = interpolationBeforeRewind;
        }

        private int FindFirstFrameAtOrAfter(float timelineTime)
        {
            int low = 0;
            int high = frames.Count - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (frames[middle].Time < timelineTime)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private int FindFirstFrameAfter(float timelineTime)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].Time > timelineTime + 0.0001f)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
