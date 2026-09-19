using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CutsceneTrigger2D : MonoBehaviour
    {
        [SerializeField] private CutsceneSequence sequence;
        [SerializeField] private bool once = true;

        private bool played;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if ((once && played) || sequence == null || other.GetComponentInParent<PlayerMotor2D>() == null)
            {
                return;
            }

            played = true;
            sequence.Play();
        }
    }
}
