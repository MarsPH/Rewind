using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho.Flow
{
    public sealed class FlowEventRelay : MonoBehaviour
    {
        [SerializeField] private UnityEvent onAnyTransitionStarted;
        [SerializeField] private UnityEvent onAnyTransitionFinished;
        [SerializeField] private UnityEvent onNextLevelTransitionStarted;
        [SerializeField] private UnityEvent onDeathReloadTransitionStarted;
        [SerializeField] private UnityEvent onRestartTransitionStarted;
        [SerializeField] private UnityEvent onLevelEntered;
        [SerializeField] private UnityEvent onPlayerDied;
        [SerializeField] private UnityEvent onWon;

        private TimeEchoFlowManager subscribed;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            TimeEchoFlowManager manager = TimeEchoFlowManager.Instance;
            if (manager == null || manager == subscribed) return;
            Unsubscribe();
            subscribed = manager;
            subscribed.TransitionStarted += HandleTransitionStarted;
            subscribed.TransitionFinished += HandleTransitionFinished;
            subscribed.LevelEntered += HandleLevelEntered;
            subscribed.PlayerDied += HandlePlayerDied;
            subscribed.StateChanged += HandleStateChanged;
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.TransitionStarted -= HandleTransitionStarted;
            subscribed.TransitionFinished -= HandleTransitionFinished;
            subscribed.LevelEntered -= HandleLevelEntered;
            subscribed.PlayerDied -= HandlePlayerDied;
            subscribed.StateChanged -= HandleStateChanged;
            subscribed = null;
        }

        private void HandleTransitionStarted(FlowTransitionReason reason, string sceneName)
        {
            onAnyTransitionStarted?.Invoke();
            if (reason == FlowTransitionReason.NextLevel) onNextLevelTransitionStarted?.Invoke();
            if (reason == FlowTransitionReason.Death) onDeathReloadTransitionStarted?.Invoke();
            if (reason == FlowTransitionReason.Restart) onRestartTransitionStarted?.Invoke();
        }
        private void HandleTransitionFinished(FlowTransitionReason reason, string sceneName) => onAnyTransitionFinished?.Invoke();
        private void HandleLevelEntered(int index, string sceneName) => onLevelEntered?.Invoke();
        private void HandlePlayerDied(int deathCount) => onPlayerDied?.Invoke();

        private void HandleStateChanged(FlowState previous, FlowState next)
        {
            if (next == FlowState.Won) onWon?.Invoke();
        }
    }
}
