using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class GuidanceTrigger2D : MonoBehaviour
    {
        [TextArea(2, 5)] [SerializeField] private string message = "Hold RMB to rewind time.";
        [SerializeField, Min(0f)] private float duration = 3f;
        [SerializeField] private bool showOnlyOnce = true;

        private bool shown;

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if ((shown && showOnlyOnce) || other.GetComponentInParent<PlayerMotor2D>() == null)
            {
                return;
            }

            shown = true;
            GuidanceDirector.Instance?.Show(message, duration);
        }
    }
}
