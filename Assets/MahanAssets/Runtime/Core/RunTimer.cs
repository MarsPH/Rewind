using System;
using UnityEngine;

namespace TimeEcho
{
    public sealed class RunTimer : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private TimeDirector timeDirector;

        private bool running;
        private float elapsed;

        public float Elapsed => tuning != null && tuning.timer.policy == TimerPolicy.RewindableTimeline && timeDirector != null
            ? timeDirector.Timeline
            : elapsed;

        public event Action<float> Changed;

        private void Awake()
        {
            if (timeDirector == null)
            {
                timeDirector = FindObjectOfType<TimeDirector>();
            }

            running = tuning == null || tuning.timer.beginOnSceneLoad;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            TimerPolicy policy = tuning != null ? tuning.timer.policy : TimerPolicy.FlowingWorldTime;
            switch (policy)
            {
                case TimerPolicy.RealAttemptTime:
                    elapsed += Time.unscaledDeltaTime;
                    break;
                case TimerPolicy.FlowingWorldTime:
                    if (timeDirector == null || timeDirector.Mode == TimeMode.Flowing)
                    {
                        elapsed += Time.deltaTime;
                    }
                    break;
                case TimerPolicy.RewindableTimeline:
                    break;
            }

            Changed?.Invoke(Elapsed);
        }

        public void Configure(GameTuning gameTuning, TimeDirector director)
        {
            tuning = gameTuning;
            timeDirector = director;
        }

        public void StartTimer()
        {
            running = true;
        }

        public void StopTimer()
        {
            running = false;
        }

        public void ResetTimer()
        {
            elapsed = 0f;
            Changed?.Invoke(Elapsed);
        }
    }
}
