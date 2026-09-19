using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    public sealed class ProjectileLauncher : MonoBehaviour
    {
        [SerializeField] private Projectile2D projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(1)] private int prewarmCount = 8;
        [SerializeField, Min(0f)] private float cooldown = 0.1f;
        [SerializeField] private AudioCue fireCue;

        private readonly List<Projectile2D> pool = new List<Projectile2D>();
        private float nextFireTime;

        private void Awake()
        {
            if (projectilePrefab == null)
            {
                return;
            }

            for (int i = 0; i < prewarmCount; i++)
            {
                CreateProjectile();
            }
        }

        public bool TryFire(Vector2 direction, float speed)
        {
            if (projectilePrefab == null || Time.unscaledTime < nextFireTime || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Projectile2D projectile = GetAvailableProjectile();
            Vector3 position = muzzle != null ? muzzle.position : transform.position;
            projectile.Launch(position, direction.normalized, speed, gameObject);
            nextFireTime = Time.unscaledTime + cooldown;
            AudioService.Instance?.Play(fireCue, position);
            return true;
        }

        private Projectile2D GetAvailableProjectile()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                {
                    return pool[i];
                }
            }

            return CreateProjectile();
        }

        private Projectile2D CreateProjectile()
        {
            Projectile2D instance = Instantiate(projectilePrefab, transform);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
            return instance;
        }
    }
}
