using UnityEngine;

namespace TimeEcho.Flow
{
    public sealed class PlayerDeathReporter : MonoBehaviour
    {
        [SerializeField] private TimeEcho.PlayerVitality vitality;

        private void Reset()
        {
            vitality = GetComponentInParent<TimeEcho.PlayerVitality>();
        }

        private void OnEnable()
        {
            if (vitality == null)
            {
                vitality = GetComponentInParent<TimeEcho.PlayerVitality>();
            }

            if (vitality != null)
            {
                vitality.Died += Report;
                TimeEchoFlowManager.Instance?.WatchVitality(vitality);
            }
        }

        private void OnDisable()
        {
            if (vitality != null)
            {
                vitality.Died -= Report;
            }
        }

        public void Report()
        {
            TimeEchoFlowManager.Instance?.ReportPlayerDeath();
        }
    }
}
