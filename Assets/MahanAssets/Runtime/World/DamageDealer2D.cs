using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class DamageDealer2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0f)] private float knockback = 7f;
        [SerializeField] private bool trigger = true;
        [SerializeField] private AudioCue impactCue;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = trigger;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (trigger) TryDamage(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!trigger) TryDamage(collision.collider);
        }

        private void TryDamage(Collider2D other)
        {
            PlayerVitality target = other.GetComponentInParent<PlayerVitality>();
            if (target == null)
            {
                return;
            }

            Vector2 direction = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            if (!target.ApplyDamage(damage, other.ClosestPoint(transform.position), direction))
            {
                return;
            }

            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body != null && knockback > 0f)
            {
                body.AddForce(direction * knockback, ForceMode2D.Impulse);
            }

            AudioService.Instance?.Play(impactCue, transform.position);
        }
    }
}
