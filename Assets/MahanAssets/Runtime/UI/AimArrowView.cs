using UnityEngine;

namespace TimeEcho
{
    public sealed class AimArrowView : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private LineRenderer shaft;
        [SerializeField] private LineRenderer head;

        private void Awake()
        {
            if (shaft == null)
            {
                shaft = GetComponent<LineRenderer>();
            }

            Hide();
        }

        public void Configure(GameTuning gameTuning, LineRenderer shaftRenderer, LineRenderer headRenderer)
        {
            tuning = gameTuning;
            shaft = shaftRenderer;
            head = headRenderer;
            ApplyStaticAppearance();
            Hide();
        }

        public void Show(Vector2 origin, Vector2 direction, float charge01, bool stasis, bool ready = true)
        {
            if (shaft == null || head == null)
            {
                return;
            }

            AimTuning aim = tuning != null ? tuning.aim : null;
            float minimumLength = aim != null ? aim.minimumArrowLength : 0.8f;
            float maximumLength = aim != null ? aim.maximumArrowLength : 4.5f;
            float headSize = aim != null ? aim.arrowHeadSize : 0.28f;
            float length = Mathf.Lerp(minimumLength, maximumLength, Mathf.Clamp01(charge01));
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 end = origin + normalizedDirection * length;
            Vector2 side = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            Vector2 headBase = end - normalizedDirection * headSize;

            Color color = stasis
                ? (aim != null ? aim.stasisArrowColor : Color.yellow)
                : (aim != null ? aim.normalArrowColor : Color.cyan);
            if (!ready)
            {
                // A preview is visible immediately, but a dim arrow means
                // releasing right now will cancel rather than launch.
                color.a *= 0.4f;
            }

            shaft.enabled = true;
            head.enabled = true;
            shaft.positionCount = 2;
            shaft.SetPosition(0, origin);
            shaft.SetPosition(1, end);
            head.positionCount = 3;
            head.SetPosition(0, headBase + side * headSize * 0.55f);
            head.SetPosition(1, end);
            head.SetPosition(2, headBase - side * headSize * 0.55f);
            shaft.startColor = shaft.endColor = color;
            head.startColor = head.endColor = color;
        }

        public void Hide()
        {
            if (shaft != null) shaft.enabled = false;
            if (head != null) head.enabled = false;
        }

        private void ApplyStaticAppearance()
        {
            float width = tuning != null ? tuning.aim.lineWidth : 0.08f;
            if (shaft != null)
            {
                shaft.useWorldSpace = true;
                shaft.alignment = LineAlignment.TransformZ;
                shaft.textureMode = LineTextureMode.Stretch;
                shaft.numCapVertices = 2;
                shaft.startWidth = width;
                shaft.endWidth = width;
                shaft.sortingOrder = 50;
            }

            if (head != null)
            {
                head.useWorldSpace = true;
                head.alignment = LineAlignment.TransformZ;
                head.numCapVertices = 2;
                head.startWidth = width;
                head.endWidth = width;
                head.sortingOrder = 50;
            }
        }
    }
}
