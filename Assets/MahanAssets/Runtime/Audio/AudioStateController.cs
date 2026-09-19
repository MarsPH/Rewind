using UnityEngine;
using UnityEngine.Audio;

namespace TimeEcho
{
    public sealed class AudioStateController : MonoBehaviour
    {
        [SerializeField] private TimeDirector timeDirector;
        [SerializeField] private AudioMixerSnapshot flowingSnapshot;
        [SerializeField] private AudioMixerSnapshot rewindSnapshot;
        [SerializeField] private AudioMixerSnapshot stasisSnapshot;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;

        private void Awake()
        {
            if (timeDirector == null) timeDirector = FindObjectOfType<TimeDirector>();
        }

        private void OnEnable()
        {
            if (timeDirector != null) timeDirector.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            if (timeDirector != null) timeDirector.ModeChanged -= OnModeChanged;
        }

        public void Configure(TimeDirector director)
        {
            timeDirector = director;
        }

        private void OnModeChanged(TimeMode previous, TimeMode current)
        {
            AudioMixerSnapshot snapshot = current == TimeMode.Rewinding
                ? rewindSnapshot
                : current == TimeMode.Stasis
                    ? stasisSnapshot
                    : flowingSnapshot;
            snapshot?.TransitionTo(transitionSeconds);
        }
    }
}
