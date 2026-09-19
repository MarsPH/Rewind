using UnityEngine;

namespace TimeEcho
{
    [System.Obsolete("Use StaticLevelCamera2D. This compatibility component now frames a fixed level view and does not follow its target.")]
    [ExecuteAlways]
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

        private void OnEnable()
        {
            controlledCamera = GetComponent<Camera>();
            FrameWholeLevel();
        }

        private void LateUpdate()
        {
            ApplyFrame(false);
        }

        [ContextMenu("Frame Whole Level")]
        public void FrameWholeLevel()
        {
            ApplyFrame(true);
        }

        private void ApplyFrame(bool reportConfigurationErrors)
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            if (controlledCamera == null || !controlledCamera.orthographic)
            {
                if (reportConfigurationErrors)
                {
                    Debug.LogError("The static level camera requires an orthographic Camera.", this);
                }

                return;
            }

            float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
            float verticalHalfSize = Mathf.Max(levelSize.y * 0.5f, levelSize.x * 0.5f / aspect);
            float desiredSize = verticalHalfSize + padding;
            Vector3 desiredPosition = new Vector3(levelCenter.x, levelCenter.y, cameraDepth);

            if (!Mathf.Approximately(controlledCamera.orthographicSize, desiredSize))
            {
                controlledCamera.orthographicSize = desiredSize;
            }

            if ((transform.position - desiredPosition).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.001f)
            {
                transform.SetPositionAndRotation(desiredPosition, Quaternion.identity);
            }
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            FrameWholeLevel();
        }

        private void OnValidate()
        {
            levelSize.x = Mathf.Max(0.1f, levelSize.x);
            levelSize.y = Mathf.Max(0.1f, levelSize.y);
            padding = Mathf.Max(0f, padding);
            ApplyFrame(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(levelCenter, levelSize);
        }
    }
}
