using UnityEngine;

namespace TimeEcho
{
    public sealed class FollowCamera2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float smoothTime = tuning != null ? tuning.feedback.cameraFollowSmoothTime : 0.12f;
            Vector3 desired = target.position + offset;
            desired.z = offset.z;
            transform.position = smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        public void Configure(Transform followTarget, GameTuning gameTuning)
        {
            target = followTarget;
            tuning = gameTuning;
        }
    }
}
