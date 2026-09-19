using UnityEngine;

namespace TimeEcho.Flow
{
    [CreateAssetMenu(fileName = "GameMasterSequence", menuName = "Time Echo/Progression/Sequence")]
    public sealed class FlowSequenceAsset : ScriptableObject
    {
        public FlowSequence sequence = new FlowSequence();
    }
}
