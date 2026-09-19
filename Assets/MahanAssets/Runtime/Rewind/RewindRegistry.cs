using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    public static class RewindRegistry
    {
        private static readonly List<IRewindable> Items = new List<IRewindable>(64);

        public static void Register(IRewindable item)
        {
            if (item != null && !Items.Contains(item))
            {
                Items.Add(item);
            }
        }

        public static void Unregister(IRewindable item)
        {
            Items.Remove(item);
        }

        public static void CaptureAll(float timelineTime)
        {
            ForEach(item => item.Capture(timelineTime));
        }

        public static void RestoreAll(float timelineTime)
        {
            ForEach(item => item.Restore(timelineTime));
        }

        public static void TrimFutureAll(float timelineTime)
        {
            ForEach(item => item.TrimFuture(timelineTime));
        }

        public static void BeginRewindAll()
        {
            ForEach(item => item.BeginRewind());
        }

        public static void EndRewindAll()
        {
            ForEach(item => item.EndRewind());
        }

        private static void ForEach(System.Action<IRewindable> action)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                IRewindable item = Items[i];
                Object unityObject = item as Object;
                if (item == null || unityObject == null)
                {
                    Items.RemoveAt(i);
                    continue;
                }

                action(item);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnDomainReload()
        {
            Items.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebuildAfterSceneLoad()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour.isActiveAndEnabled && behaviour is IRewindable rewindable)
                {
                    Register(rewindable);
                }
            }
        }
    }
}
