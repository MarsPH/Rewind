using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho.Flow
{
    [Serializable]
    public sealed class OpeningCutsceneSlide
    {
        public Sprite image;
        [TextArea(2, 7)] public string text;
        public AudioClip voiceClip;
        [Min(0.05f)] public float duration = 2.5f;
        public Color backgroundColor = Color.black;
        public Color textColor = Color.white;
        public bool preserveImageAspect = true;

        public float RequiredDuration(bool waitForVoice)
        {
            float voiceDuration = waitForVoice && voiceClip != null ? voiceClip.length : 0f;
            return Mathf.Max(duration, voiceDuration);
        }
    }

    [CreateAssetMenu(fileName = "OpeningCutscene", menuName = "Time Echo/Progression/Opening Cutscene")]
    public sealed class OpeningCutsceneAsset : ScriptableObject
    {
        public bool enabled = true;
        public List<OpeningCutsceneSlide> slides = new List<OpeningCutsceneSlide>();

        [Header("Transitions")]
        [Min(0f)] public float fadeInSeconds = 0.4f;
        [Min(0f)] public float fadeOutSeconds = 0.4f;

        [Header("Skipping")]
        public bool allowSkip = true;
        public KeyCode skipKey = KeyCode.Space;
        public string skipHint = "Press Space to skip";

        [Header("Audio")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        public bool loopMusic = true;
        public bool waitForVoiceToFinish = true;

        public bool CanPlay => enabled && slides != null && slides.Count > 0;
    }
}
