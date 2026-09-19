using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho.Flow
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExitTrigger2D : MonoBehaviour
    {
        [SerializeField] private bool requirePlayerTag = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requirePlayerVitality = true;
        [SerializeField] private bool requireAllCollectibles;
        [Tooltip("Leave empty to follow the order in TimeEchoFlowConfig.")]
        [SerializeField] private string destinationSceneOverride;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private UnityEvent onExitAccepted;
        [SerializeField] private UnityEvent onExitRejected;

        private bool used;

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (used || other == null || !IsPlayer(other))
            {
                return;
            }

            if (requireAllCollectibles && TimeEcho.GameSession.Instance != null &&
                TimeEcho.GameSession.Instance.CollectedCount < TimeEcho.GameSession.Instance.TotalCollectibles)
            {
                onExitRejected?.Invoke();
                return;
            }

            TimeEchoFlowManager manager = TimeEchoFlowManager.Instance;
            if (manager == null || manager.IsBusy)
            {
                onExitRejected?.Invoke();
                return;
            }

            used = oneShot;
            onExitAccepted?.Invoke();
            manager.CompleteCurrentLevel(destinationSceneOverride);
        }

        public void ResetTrigger()
        {
            used = false;
        }

        private bool IsPlayer(Collider2D other)
        {
            GameObject candidate = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (requirePlayerTag && !candidate.CompareTag(playerTag) && !other.CompareTag(playerTag))
            {
                return false;
            }

            if (requirePlayerVitality && candidate.GetComponentInParent<TimeEcho.PlayerVitality>() == null)
            {
                return false;
            }

            return true;
        }
    }
}
