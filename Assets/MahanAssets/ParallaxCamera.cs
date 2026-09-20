using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ParallaxCamera : MonoBehaviour
{
    public delegate void ParallaxCameraDelegate(float deltaMovement);
    public ParallaxCameraDelegate onCameraTranslate;

    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 targetOffset;
    [SerializeField] private bool followHorizontal = true;
    [SerializeField] private bool followVertical = true;
    [SerializeField, Min(0f)] private float smoothTime = 0.12f;
    [Tooltip("Normally disabled so arranging objects in the Scene view does not move the camera.")]
    [SerializeField] private bool followInEditMode;

    [Header("Room Boundary")]
    [Tooltip("Assign a BoxCollider2D that covers the full area the camera is allowed to show.")]
    [SerializeField] private BoxCollider2D levelBounds;
    [Tooltip("Extra distance kept between the camera edges and the boundary.")]
    [SerializeField, Min(0f)] private float boundsPadding;

    [Header("Camera")]
    [SerializeField] private float cameraDepth = -10f;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private float oldPositionX;
    private bool warnedAboutCameraType;
    private bool warnedAboutSmallBounds;

    public Transform Target => target;
    public BoxCollider2D LevelBounds => levelBounds;

    private void OnEnable()
    {
        CacheCamera();
        oldPositionX = transform.position.x;
        followVelocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        CacheCamera();

        if (Application.isPlaying || followInEditMode)
        {
            FollowAndClamp();
        }

        NotifyParallaxLayers();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        followVelocity = Vector3.zero;
    }

    public void SetLevelBounds(BoxCollider2D newBounds)
    {
        levelBounds = newBounds;
        warnedAboutSmallBounds = false;
        ClampCurrentPosition();
    }

    [ContextMenu("Snap To Target Inside Bounds")]
    public void SnapToTargetInsideBounds()
    {
        CacheCamera();
        followVelocity = Vector3.zero;

        Vector3 desired = GetDesiredPosition();
        desired = ClampPosition(desired, true);
        transform.position = desired;
        oldPositionX = transform.position.x;
    }

    [ContextMenu("Clamp Current Position")]
    public void ClampCurrentPosition()
    {
        CacheCamera();
        transform.position = ClampPosition(transform.position, false);
        followVelocity = Vector3.zero;
        oldPositionX = transform.position.x;
    }

    private void FollowAndClamp()
    {
        if (controlledCamera == null || !controlledCamera.orthographic)
        {
            if (!warnedAboutCameraType)
            {
                Debug.LogWarning("ParallaxCamera bounded follow requires an orthographic Camera.", this);
                warnedAboutCameraType = true;
            }
            return;
        }

        warnedAboutCameraType = false;
        Vector3 desired = ClampPosition(GetDesiredPosition(), true);

        Vector3 nextPosition;
        if (!Application.isPlaying || smoothTime <= 0.0001f)
        {
            nextPosition = desired;
        }
        else
        {
            nextPosition = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        // A second clamp guarantees smoothing can never expose space outside the room,
        // even when the camera began beyond the valid area.
        transform.position = ClampPosition(nextPosition, false);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 current = transform.position;
        if (target != null)
        {
            if (followHorizontal)
            {
                current.x = target.position.x + targetOffset.x;
            }
            if (followVertical)
            {
                current.y = target.position.y + targetOffset.y;
            }
        }

        current.z = cameraDepth;
        return current;
    }

    private Vector3 ClampPosition(Vector3 position, bool reportSmallBounds)
    {
        position.z = cameraDepth;
        if (controlledCamera == null || !controlledCamera.orthographic || levelBounds == null)
        {
            return position;
        }

        Bounds bounds = levelBounds.bounds;
        float halfHeight = controlledCamera.orthographicSize;
        float halfWidth = halfHeight * Mathf.Max(0.01f, controlledCamera.aspect);

        float minimumX = bounds.min.x + halfWidth + boundsPadding;
        float maximumX = bounds.max.x - halfWidth - boundsPadding;
        float minimumY = bounds.min.y + halfHeight + boundsPadding;
        float maximumY = bounds.max.y - halfHeight - boundsPadding;

        bool tooNarrow = minimumX > maximumX;
        bool tooShort = minimumY > maximumY;

        // When the viewport is larger than the room on one axis, no camera position
        // can hide the outside. Center that axis instead of allowing unstable clamping.
        position.x = tooNarrow ? bounds.center.x : Mathf.Clamp(position.x, minimumX, maximumX);
        position.y = tooShort ? bounds.center.y : Mathf.Clamp(position.y, minimumY, maximumY);

        if (reportSmallBounds && (tooNarrow || tooShort) && !warnedAboutSmallBounds)
        {
            Debug.LogWarning(
                "The camera viewport is larger than Camera Bounds on at least one axis. " +
                "Lower the Camera Orthographic Size or enlarge the bounds if outside space is still visible.",
                this);
            warnedAboutSmallBounds = true;
        }
        else if (!tooNarrow && !tooShort)
        {
            warnedAboutSmallBounds = false;
        }

        return position;
    }

    private void NotifyParallaxLayers()
    {
        float currentPositionX = transform.position.x;
        if (Mathf.Approximately(currentPositionX, oldPositionX))
        {
            return;
        }

        float delta = oldPositionX - currentPositionX;
        onCameraTranslate?.Invoke(delta);
        oldPositionX = currentPositionX;
    }

    private void CacheCamera()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0f, smoothTime);
        boundsPadding = Mathf.Max(0f, boundsPadding);
        CacheCamera();

        if (!Application.isPlaying && followInEditMode)
        {
            SnapToTargetInsideBounds();
        }
    }

    private void OnDrawGizmosSelected()
    {
        CacheCamera();
        if (controlledCamera == null || !controlledCamera.orthographic || levelBounds == null)
        {
            return;
        }

        Bounds bounds = levelBounds.bounds;
        float halfHeight = controlledCamera.orthographicSize;
        float halfWidth = halfHeight * Mathf.Max(0.01f, controlledCamera.aspect);
        float safeWidth = Mathf.Max(0f, bounds.size.x - 2f * (halfWidth + boundsPadding));
        float safeHeight = Mathf.Max(0f, bounds.size.y - 2f * (halfHeight + boundsPadding));

        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(bounds.center.x, bounds.center.y, transform.position.z),
            new Vector3(safeWidth, safeHeight, 0f));
    }
}
