using UnityEngine;

namespace TimeEcho.Flow
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GameMasterSequenceTrigger2D : MonoBehaviour
    {
        [SerializeField] private FlowSequenceAsset sequenceAsset;
        [SerializeField] private bool useLocalSequence;
        [SerializeField] private FlowSequence localSequence = new FlowSequence();
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool oneShot = true;

        private bool used;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (used || other == null) return;
            GameObject candidate = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!candidate.CompareTag(playerTag) && !other.CompareTag(playerTag)) return;
            FlowSequence sequence = useLocalSequence ? localSequence : sequenceAsset != null ? sequenceAsset.sequence : null;
            if (sequence == null || !sequence.enabled) return;
            used = oneShot;
            TimeEchoFlowManager.Instance?.PlaySequence(sequence);
        }

        public void ResetTrigger()
        {
            used = false;
        }
    }
}
