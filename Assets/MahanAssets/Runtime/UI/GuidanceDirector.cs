using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TimeEcho
{
    public sealed class GuidanceDirector : MonoBehaviour
    {
        public static GuidanceDirector Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text label;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.2f;

        private Coroutine routine;

        private void Awake()
        {
            Instance = this;
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(CanvasGroup targetGroup, Text targetLabel)
        {
            group = targetGroup;
            label = targetLabel;
            SetAlpha(0f);
        }

        public void Show(string message, float duration = 3f)
        {
            if (label == null || group == null)
            {
                return;
            }

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(message, duration));
        }

        public void Hide()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Fade(group != null ? group.alpha : 0f, 0f));
        }

        private IEnumerator ShowRoutine(string message, float duration)
        {
            label.text = message;
            yield return Fade(group.alpha, 1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            yield return Fade(group.alpha, 0f);
            routine = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeSeconds <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / fadeSeconds));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float value)
        {
            if (group == null) return;
            group.alpha = value;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

}
