using UnityEngine;
using System;
using System.Collections.Generic;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 번들/팩 시스템.
    /// 시간 제한 번들, 스타터 팩, 한정 패키지를 관리한다.
    /// IAP 구매 성공 이벤트를 구독하여 보상을 자동 지급한다.
    /// </summary>
    public class BundleSystem : MonoBehaviour
    {
        public static BundleSystem Instance { get; private set; }

        [System.Serializable]
        public class BundleData
        {
            public string bundleId;
            public string displayName;
            public string productId; // IAPManager product
            public int maxPurchases; // 0 = unlimited
            public float discountPercent;
            public BundleReward[] rewards;
            public bool isTimeLimited;
            public int durationHours; // 0 = permanent
        }

        [System.Serializable]
        public class BundleReward
        {
            public CurrencyType currencyType;
            public int amount;
            public string itemId;
        }

        [SerializeField] private BundleData[] _bundles;

        private readonly Dictionary<string, int> _purchaseCounts = new();
        private readonly Dictionary<string, DateTime> _bundleExpiry = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            EventBus<IAPPurchaseEvent>.Subscribe(OnIAPPurchase);
        }

        private void OnDisable()
        {
            EventBus<IAPPurchaseEvent>.Unsubscribe(OnIAPPurchase);
        }

        public BundleData GetBundle(string bundleId)
        {
            if (_bundles == null) return null;
            for (int i = 0; i < _bundles.Length; i++)
                if (_bundles[i].bundleId == bundleId) return _bundles[i];
            return null;
        }

        public List<BundleData> GetAvailableBundles()
        {
            var result = new List<BundleData>();
            if (_bundles == null) return result;
            for (int i = 0; i < _bundles.Length; i++)
                if (IsBundleAvailable(_bundles[i].bundleId)) result.Add(_bundles[i]);
            return result;
        }

        public bool IsBundleAvailable(string bundleId)
        {
            var bundle = GetBundle(bundleId);
            if (bundle == null) return false;

            // 구매 횟수 제한
            if (bundle.maxPurchases > 0)
            {
                _purchaseCounts.TryGetValue(bundleId, out int count);
                if (count >= bundle.maxPurchases) return false;
            }

            // 시간 제한
            if (bundle.isTimeLimited && _bundleExpiry.TryGetValue(bundleId, out var expiry))
            {
                if (DateTime.UtcNow > expiry) return false;
            }

            return true;
        }

        public void ActivateTimeLimitedBundle(string bundleId)
        {
            var bundle = GetBundle(bundleId);
            if (bundle == null || !bundle.isTimeLimited) return;

            _bundleExpiry[bundleId] = DateTime.UtcNow.AddHours(bundle.durationHours);
            Debug.Log($"[BundleSystem] 한정 번들 활성화: {bundleId}, 만료: {_bundleExpiry[bundleId]}");
        }

        public int GetPurchaseCount(string bundleId)
        {
            _purchaseCounts.TryGetValue(bundleId, out int count);
            return count;
        }

        public DateTime? GetExpiry(string bundleId)
        {
            return _bundleExpiry.TryGetValue(bundleId, out var expiry) ? expiry : null;
        }

        private void OnIAPPurchase(IAPPurchaseEvent e)
        {
            if (_bundles == null) return;
            for (int i = 0; i < _bundles.Length; i++)
            {
                if (_bundles[i].productId == e.ProductId)
                {
                    GrantBundleRewards(_bundles[i]);
                    break;
                }
            }
        }

        private void GrantBundleRewards(BundleData bundle)
        {
            if (bundle.rewards == null) return;

            for (int i = 0; i < bundle.rewards.Length; i++)
            {
                var reward = bundle.rewards[i];
                if (reward.amount > 0)
                    CurrencyManager.Instance?.Add(reward.currencyType, reward.amount);
            }

            _purchaseCounts.TryGetValue(bundle.bundleId, out int count);
            _purchaseCounts[bundle.bundleId] = count + 1;

            Debug.Log($"[BundleSystem] 번들 보상 지급: {bundle.displayName}");
        }
    }
}
