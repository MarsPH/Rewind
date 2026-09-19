using UnityEngine;

namespace TimeEcho
{
    [System.Obsolete("Use StaticLevelCamera2D. This compatibility component now frames a fixed level view and does not follow its target.")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera2D : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform target;
        [SerializeField] private Vector2 levelCenter = new Vector2(0f, 0.5f);
        [SerializeField] private Vector2 levelSize = new Vector2(24f, 9f);
        [SerializeField, Min(0f)] private float padding = 0.5f;
        [SerializeField] private float cameraDepth = -10f;

        private Camera controlledCamera;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            FrameWholeLevel();
        }

        [ContextMenu("Frame Whole Level")]
        public void FrameWholeLevel()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            if (controlledCamera == null || !controlledCamera.orthographic)
            {
                Debug.LogError("The static level camera requires an orthographic Camera.", this);
                return;
            }

            float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
            float verticalHalfSize = Mathf.Max(levelSize.y * 0.5f, levelSize.x * 0.5f / aspect);
            controlledCamera.orthographicSize = verticalHalfSize + padding;
            transform.SetPositionAndRotation(
                new Vector3(levelCenter.x, levelCenter.y, cameraDepth),
                Quaternion.identity);
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(levelCenter, levelSize);
        }
    }
}
