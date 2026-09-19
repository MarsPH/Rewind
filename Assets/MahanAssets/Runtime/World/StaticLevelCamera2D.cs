using UnityEngine;

namespace TimeEcho
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class StaticLevelCamera2D : MonoBehaviour
    {
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
            // ExecuteAlways keeps the edit-time Game view identical to Play Mode.
            // The comparison inside ApplyFrame also handles Game view aspect changes
            // without moving the camera relative to the player.
            ApplyFrame(false);
        }

        public void Configure(Vector2 center, Vector2 size, float framingPadding = 0.5f)
        {
            levelCenter = center;
            levelSize = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            padding = Mathf.Max(0f, framingPadding);
            FrameWholeLevel();
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
                    Debug.LogError("StaticLevelCamera2D requires an orthographic Camera.", this);
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
