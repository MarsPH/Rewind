using UnityEngine;
using UnityEngine.Audio;

namespace TimeEcho
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Time Echo/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        // Keep the original clip list serialized so existing user-authored cues retain their references.
        [SerializeField] private AudioClip[] clips;
#if UNITY_6000_0_OR_NEWER
        [Header("Unity 6 audio (overrides Clips)")]
        [Tooltip("Assign an Audio Random Container or AudioClip. When populated, Clips are ignored. Configure container volume and pitch inside the container.")]
        [SerializeField] private AudioResource audioResource;
#endif
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField, Min(0f)] private float minimumRepeatDelay;

        public AudioMixerGroup Output => output;
        public float SpatialBlend => spatialBlend;
        public float MinimumRepeatDelay => minimumRepeatDelay;
        public bool HasClip
        {
            get
            {
                if (clips == null) return false;
                foreach (AudioClip clip in clips)
                {
                    if (clip != null) return true;
                }
                return false;
            }
        }

        public bool HasAudio
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                if (audioResource != null) return true;
#endif
                return HasClip;
            }
        }

#if UNITY_6000_0_OR_NEWER
        // Unity exposes AudioRandomContainer as an internal class in some releases.
        // Every non-clip AudioResource is treated as container-managed audio.
        public bool UsesRandomContainer => audioResource != null && !(audioResource is AudioClip);

        public AudioResource PickResource()
        {
            return audioResource != null ? audioResource : PickClip();
        }
#endif

        public AudioClip PickClip()
        {
            if (!HasClip)
            {
                return null;
            }

            // Ignore empty slots rather than randomly choosing silence.
            int populatedCount = 0;
            foreach (AudioClip clip in clips)
            {
                if (clip != null) populatedCount++;
            }

            int selected = Random.Range(0, populatedCount);
            foreach (AudioClip clip in clips)
            {
                if (clip == null) continue;
                if (selected-- == 0) return clip;
            }
            return null;
        }

        public float PickVolume()
        {
            float low = Mathf.Min(volumeRange.x, volumeRange.y);
            float high = Mathf.Max(volumeRange.x, volumeRange.y);
            return Random.Range(low, high);
        }

        public float PickPitch()
        {
            float low = Mathf.Min(pitchRange.x, pitchRange.y);
            float high = Mathf.Max(pitchRange.x, pitchRange.y);
            return Random.Range(low, high);
        }
    }
}
