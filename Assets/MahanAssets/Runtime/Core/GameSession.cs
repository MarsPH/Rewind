using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeEcho
{
    [DefaultExecutionOrder(-900)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public event Action<int, int> CollectibleCountChanged;
        public event Action AllCollectiblesCollected;

        private readonly HashSet<string> registeredCollectibles = new HashSet<string>();
        private readonly HashSet<string> collectedCollectibles = new HashSet<string>();

        public int CollectedCount => collectedCollectibles.Count;
        public int TotalCollectibles => registeredCollectibles.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GameSession may exist in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RegisterCollectible(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || !registeredCollectibles.Add(stableId))
            {
                return;
            }

            RaiseCollectibleCountChanged();
        }

        public void SetCollected(string stableId, bool collected)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return;
            }

            registeredCollectibles.Add(stableId);
            bool changed = collected
                ? collectedCollectibles.Add(stableId)
                : collectedCollectibles.Remove(stableId);

            if (!changed)
            {
                return;
            }

            RaiseCollectibleCountChanged();
            if (registeredCollectibles.Count > 0 && collectedCollectibles.Count == registeredCollectibles.Count)
            {
                AllCollectiblesCollected?.Invoke();
            }
        }

        private void RaiseCollectibleCountChanged()
        {
            CollectibleCountChanged?.Invoke(CollectedCount, TotalCollectibles);
        }
    }
}
