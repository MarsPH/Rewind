using UnityEngine;
using UnityEngine.Audio;

namespace TimeEcho
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Time Echo/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField, Min(0f)] private float minimumRepeatDelay;

        public AudioMixerGroup Output => output;
        public float SpatialBlend => spatialBlend;
        public float MinimumRepeatDelay => minimumRepeatDelay;
        public bool HasClip => clips != null && clips.Length > 0;

        public AudioClip PickClip()
        {
            if (!HasClip)
            {
                return null;
            }

            return clips[Random.Range(0, clips.Length)];
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
