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
        public GameTuning Tuning => tuning;
        public float Maximum => tuning != null ? tuning.vitality.maximum : Mathf.Max(1f, startingVitality);
        public float Normalized => Maximum <= 0f ? 0f : Current / Maximum;

        // Running out of ability charges must NOT kill the player. Only lethal
        // damage causes death; zero energy merely prevents another boost.
        private bool killedByDamage;
        public bool IsDead => killedByDamage;

        public event Action<float, float> Changed;
        public event Action Died;
        public event Action Revived;
        public event Action<float, Vector2> Damaged;

        private void Awake()
        {
            killedByDamage = false;
            if (tuning != null && tuning.vitality != null && tuning.aim != null)
            {
                float cost = Maximum * Mathf.Clamp01(tuning.aim.boostVitalityCostFraction);
                if (cost > 0.0001f)
                {
                    int charges = Mathf.Max(0, tuning.vitality.startingBoostCharges);
                    float initial = charges * cost;
                    // If this project is configured to reserve 1 health point,
                    // allow the requested number of boosts to be spent without
                    // violating that rule.
                    if (charges > 0 && !tuning.vitality.boostCanReduceToZero)
                        initial += Mathf.Min(1f, Maximum);
                    Current = Mathf.Clamp(initial, 0f, Maximum);
                    return;
                }
            }

            // No configured boost cost: preserve the original vitality setup.
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
            // Rewinding is invulnerable. This also returns false to damage sources
            // so they cannot apply knockback or impact sound during rewind.
            if (TimeDirector.Instance != null && TimeDirector.Instance.Mode == TimeMode.Rewinding)
            {
                return false;
            }

            if (amount <= 0f || IsDead)
            {
                return false;
            }

            SetCurrent(Current - amount, true);
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

        private void SetCurrent(float value, bool fromDamage = false)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(value, 0f, Maximum);

            if (fromDamage && Current <= 0f)
                killedByDamage = true;
            else if (Current > 0f)
                killedByDamage = false;

            Changed?.Invoke(Current, Maximum);
            if (!wasDead && IsDead)
                Died?.Invoke();
        }
    }
}
