using System.Collections.Generic;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Audio;
#endif

namespace TimeEcho
{
    [DefaultExecutionOrder(-700)]
    public sealed class AudioService : MonoBehaviour
    {
        private sealed class ActiveVoice
        {
            public AudioSource Source;
            public Transform Follow;
            public bool Loop;
        }

        public static AudioService Instance { get; private set; }

        [SerializeField, Min(1)] private int initialVoiceCount = 12;

        private readonly List<AudioSource> idle = new List<AudioSource>();
        private readonly List<ActiveVoice> active = new List<ActiveVoice>();
        private readonly Dictionary<AudioCue, float> nextAllowedPlayTime = new Dictionary<AudioCue, float>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one AudioService may exist in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
            for (int i = 0; i < initialVoiceCount; i++)
            {
                idle.Add(CreateVoice());
            }
        }

        private void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveVoice voice = active[i];
                if (voice.Follow != null)
                {
                    voice.Source.transform.position = voice.Follow.position;
                }

                if (!voice.Loop && !voice.Source.isPlaying)
                {
                    ReleaseAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public AudioSource Play(AudioCue cue, Vector3 position)
        {
            return StartVoice(cue, position, null, false);
        }

        public AudioSource Play(AudioCue cue, Transform follow)
        {
            Vector3 position = follow != null ? follow.position : Vector3.zero;
            return StartVoice(cue, position, follow, false);
        }

        public AudioSource PlayLoop(AudioCue cue, Transform follow = null)
        {
            Vector3 position = follow != null ? follow.position : Vector3.zero;
            return StartVoice(cue, position, follow, true);
        }

        public void StopLoop(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Source == source)
                {
                    ReleaseAt(i);
                    return;
                }
            }
        }

        private AudioSource StartVoice(AudioCue cue, Vector3 position, Transform follow, bool loop)
        {
            if (cue == null || !cue.HasAudio)
            {
                return null;
            }

            float now = Time.unscaledTime;
            if (!loop && nextAllowedPlayTime.TryGetValue(cue, out float nextAllowed) && now < nextAllowed)
            {
                return null;
            }

#if UNITY_6000_0_OR_NEWER
            AudioResource resource = cue.PickResource();
            if (resource == null) return null;
#else
            AudioClip clip = cue.PickClip();
            if (clip == null) return null;
#endif

            AudioSource source = Acquire();
            source.transform.position = position;
#if UNITY_6000_0_OR_NEWER
            source.resource = resource;
            // Container pitch/volume belong to the container; don't add a second random layer.
            source.volume = cue.UsesRandomContainer ? 1f : cue.PickVolume();
            source.pitch = cue.UsesRandomContainer ? 1f : cue.PickPitch();
            // Audio Random Container looping is configured internally (Automatic + Infinite).
            source.loop = loop && !cue.UsesRandomContainer;
#else
            source.clip = clip;
            source.volume = cue.PickVolume();
            source.pitch = cue.PickPitch();
            source.loop = loop;
#endif
            source.outputAudioMixerGroup = cue.Output;
            source.spatialBlend = cue.SpatialBlend;
            source.Play();

            active.Add(new ActiveVoice { Source = source, Follow = follow, Loop = loop });
            if (!loop)
            {
                nextAllowedPlayTime[cue] = now + cue.MinimumRepeatDelay;
            }

            return source;
        }

        private AudioSource Acquire()
        {
            if (idle.Count == 0)
            {
                return CreateVoice();
            }

            int last = idle.Count - 1;
            AudioSource source = idle[last];
            idle.RemoveAt(last);
            return source;
        }

        private void ReleaseAt(int index)
        {
            AudioSource source = active[index].Source;
            active.RemoveAt(index);
            source.Stop();
#if UNITY_6000_0_OR_NEWER
            source.resource = null;
#else
            source.clip = null;
#endif
            source.loop = false;
            source.outputAudioMixerGroup = null;
            source.transform.SetParent(transform, false);
            idle.Add(source);
        }

        private AudioSource CreateVoice()
        {
            GameObject voiceObject = new GameObject("Audio Voice");
            voiceObject.transform.SetParent(transform, false);
            AudioSource source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
