using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho
{
    [Serializable]
    public sealed class CutsceneBeat
    {
        [TextArea(2, 6)] public string guidanceText;
        [Min(0f)] public float duration = 2f;
        public UnityEvent onEnter;
        public UnityEvent onExit;
    }

    public sealed class CutsceneSequence : MonoBehaviour
    {
        [SerializeField] private bool playOnStart;
        [SerializeField] private CutsceneBeat[] beats;
        [SerializeField] private UnityEvent onSequenceStarted;
        [SerializeField] private UnityEvent onSequenceFinished;

        private Coroutine routine;
        private IDisposable gameplayLock;

        public bool IsPlaying => routine != null;

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            if (routine != null || beats == null || beats.Length == 0)
            {
                return;
            }

            routine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            GuidanceDirector.Instance?.Hide();
            gameplayLock?.Dispose();
            gameplayLock = null;
        }

        private IEnumerator PlayRoutine()
        {
            gameplayLock = PresentationDirector.Instance?.AcquireGameplayLock(true);
            onSequenceStarted?.Invoke();

            for (int i = 0; i < beats.Length; i++)
            {
                CutsceneBeat beat = beats[i];
                if (beat == null) continue;
                beat.onEnter?.Invoke();
                if (!string.IsNullOrWhiteSpace(beat.guidanceText))
                {
                    GuidanceDirector.Instance?.Show(beat.guidanceText, beat.duration);
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, beat.duration));
                beat.onExit?.Invoke();
            }

            GuidanceDirector.Instance?.Hide();
            gameplayLock?.Dispose();
            gameplayLock = null;
            routine = null;
            onSequenceFinished?.Invoke();
        }
    }
}
