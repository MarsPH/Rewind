using System.Collections;
using UnityEngine;

namespace TimeEcho
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Projectile2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0.05f)] private float lifetime = 3f;
        [SerializeField] private AudioCue impactCue;

        private Rigidbody2D body;
        private GameObject owner;
        private Coroutine lifetimeRoutine;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public void Launch(Vector2 position, Vector2 direction, float speed, GameObject projectileOwner)
        {
            owner = projectileOwner;
            transform.position = position;
            transform.right = direction;
            gameObject.SetActive(true);
            body.velocity = direction * speed;

            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = StartCoroutine(DisableAfterLifetime());
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            {
                return;
            }

            IDamageable damageable = FindDamageable(other);
            damageable?.ApplyDamage(damage, transform.position, body.velocity.normalized);
            AudioService.Instance?.Play(impactCue, transform.position);
            Deactivate();
        }

        private IEnumerator DisableAfterLifetime()
        {
            yield return new WaitForSeconds(lifetime);
            Deactivate();
        }

        private void Deactivate()
        {
            if (lifetimeRoutine != null)
            {
                StopCoroutine(lifetimeRoutine);
                lifetimeRoutine = null;
            }

            body.velocity = Vector2.zero;
            gameObject.SetActive(false);
        }

        private static IDamageable FindDamageable(Collider2D other)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }
    }
}
