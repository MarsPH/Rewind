using System;
using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho
{
    public sealed class PresentationDirector : MonoBehaviour
    {
        public static PresentationDirector Instance { get; private set; }

        [SerializeField] private CanvasGroup letterbox;
        [SerializeField] private UnityEvent onPresentationStarted;
        [SerializeField] private UnityEvent onPresentationEnded;

        private int lockCount;

        public bool InputLocked => lockCount > 0;
        public event Action<bool> InputLockChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one PresentationDirector may exist in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
            ApplyLetterbox(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public IDisposable AcquireGameplayLock(bool showLetterbox = true)
        {
            lockCount++;
            if (lockCount == 1)
            {
                ApplyLetterbox(showLetterbox);
                onPresentationStarted?.Invoke();
                InputLockChanged?.Invoke(true);
            }

            return new GameplayLock(this);
        }

        public void SetCutsceneActive(bool active)
        {
            if (active)
            {
                lockCount++;
                if (lockCount == 1)
                {
                    ApplyLetterbox(true);
                    onPresentationStarted?.Invoke();
                    InputLockChanged?.Invoke(true);
                }
            }
            else
            {
                ReleaseGameplayLock();
            }
        }

        public void Configure(CanvasGroup letterboxGroup)
        {
            letterbox = letterboxGroup;
            ApplyLetterbox(InputLocked);
        }

        private void ReleaseGameplayLock()
        {
            if (lockCount <= 0)
            {
                return;
            }

            lockCount = Mathf.Max(0, lockCount - 1);
            if (lockCount != 0)
            {
                return;
            }

            ApplyLetterbox(false);
            onPresentationEnded?.Invoke();
            InputLockChanged?.Invoke(false);
        }

        private void ApplyLetterbox(bool visible)
        {
            if (letterbox == null)
            {
                return;
            }

            letterbox.alpha = visible ? 1f : 0f;
            letterbox.blocksRaycasts = visible;
            letterbox.interactable = visible;
        }

        private sealed class GameplayLock : IDisposable
        {
            private PresentationDirector owner;

            public GameplayLock(PresentationDirector owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.ReleaseGameplayLock();
                owner = null;
            }
        }
    }
}
