using System;
using UnityEngine;

namespace TimeEcho
{
    public interface IDamageable
    {
        bool ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitDirection);
    }

    public sealed class PlayerVitality : MonoBehaviour, IDamageable
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private bool beginAtMaximum = true;
        [SerializeField] private float startingVitality = 100f;

        public float Current { get; private set; }
        public float Maximum => tuning != null ? tuning.vitality.maximum : Mathf.Max(1f, startingVitality);
        public float Normalized => Maximum <= 0f ? 0f : Current / Maximum;
        public bool IsDead => Current <= 0f;

        public event Action<float, float> Changed;
        public event Action Died;
        public event Action Revived;
        public event Action<float, Vector2> Damaged;

        private void Awake()
        {
            Current = beginAtMaximum ? Maximum : Mathf.Clamp(startingVitality, 0f, Maximum);
        }

        private void Start()
        {
            Changed?.Invoke(Current, Maximum);
        }

        public void Configure(GameTuning gameTuning)
        {
            tuning = gameTuning;
            if (!Application.isPlaying)
            {
                startingVitality = Maximum;
            }
        }

        public bool ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitDirection)
        {
            if (amount <= 0f || IsDead)
            {
                return false;
            }

            SetCurrent(Current - amount);
            Damaged?.Invoke(amount, hitDirection);
            return true;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            bool wasDead = IsDead;
            SetCurrent(Current + amount);
            if (wasDead && !IsDead)
            {
                Revived?.Invoke();
            }
        }

        public void SpendTemporal(float amount, bool canReachZero)
        {
            if (amount <= 0f || IsDead)
            {
                return;
            }

            float minimum = canReachZero ? 0f : Mathf.Min(1f, Maximum);
            SetCurrent(Mathf.Max(minimum, Current - amount));
        }

        public bool CanSpendTemporal(float amount, bool canReachZero)
        {
            if (amount <= 0f)
            {
                return !IsDead;
            }

            float minimum = canReachZero ? 0f : Mathf.Min(1f, Maximum);
            return !IsDead && Current - amount >= minimum - 0.0001f;
        }

        public bool TrySpendTemporal(float amount, bool canReachZero)
        {
            if (!CanSpendTemporal(amount, canReachZero))
            {
                return false;
            }

            SpendTemporal(amount, canReachZero);
            return true;
        }

        public void RestoreToMaximum()
        {
            bool wasDead = IsDead;
            SetCurrent(Maximum);
            if (wasDead)
            {
                Revived?.Invoke();
            }
        }

        private void SetCurrent(float value)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(value, 0f, Maximum);
            Changed?.Invoke(Current, Maximum);

            if (!wasDead && IsDead)
            {
                Died?.Invoke();
            }
        }
    }
}
