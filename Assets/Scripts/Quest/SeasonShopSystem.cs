using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 시즌 상점 아이템 데이터.
    /// 시즌 포인트로 교환 가능한 보상을 정의한다.
    /// </summary>
    [System.Serializable]
    public class SeasonShopItem
    {
        public string id;
        public string displayName;
        public int seasonPointCost;
        public CurrencyType rewardType;
        public int rewardAmount;
        public int maxPurchase;
    }

    /// <summary>
    /// 시즌 포인트로 아이템을 교환하는 상점 시스템.
    /// 배틀패스 시즌 동안 획득한 시즌 포인트를 소비하여 보상을 구매한다.
    /// 시즌 리셋 시 구매 횟수가 초기화된다.
    /// </summary>
    public class SeasonShopSystem : MonoBehaviour
    {
        public static SeasonShopSystem Instance { get; private set; }

        [SerializeField] private SeasonShopItem[] _items;

        private readonly Dictionary<string, int> _purchaseCounts = new();
        private int _seasonPoints;

        /// <summary>현재 보유 시즌 포인트</summary>
        public int SeasonPoints => _seasonPoints;

        /// <summary>상점 아이템 목록</summary>
        public SeasonShopItem[] Items => _items;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 시즌 포인트를 추가한다.
        /// </summary>
        public void AddSeasonPoints(int amount)
        {
            if (amount <= 0) return;

            int previous = _seasonPoints;
            _seasonPoints += amount;

            EventBus.Publish(new SeasonPointChangedEvent
            {
                PreviousAmount = previous,
                CurrentAmount = _seasonPoints
            });
        }

        /// <summary>
        /// 해당 아이템을 구매할 수 있는지 확인한다.
        /// </summary>
        public bool CanPurchase(string itemId)
        {
            var item = FindItem(itemId);
            if (item == null) return false;

            if (_seasonPoints < item.seasonPointCost) return false;

            int count = GetPurchaseCount(itemId);
            if (item.maxPurchase > 0 && count >= item.maxPurchase) return false;

            return true;
        }

        /// <summary>
        /// 아이템을 구매한다. 시즌 포인트를 소비하고 보상을 지급한다.
        /// </summary>
        public bool Purchase(string itemId)
        {
            if (!CanPurchase(itemId)) return false;

            var item = FindItem(itemId);
            if (item == null) return false;

            // 시즌 포인트 소비
            int previous = _seasonPoints;
            _seasonPoints -= item.seasonPointCost;

            EventBus.Publish(new SeasonPointChangedEvent
            {
                PreviousAmount = previous,
                CurrentAmount = _seasonPoints
            });

            // 보상 지급
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(item.rewardType, item.rewardAmount);
            }

            // 구매 횟수 증가
            _purchaseCounts.TryGetValue(itemId, out int count);
            _purchaseCounts[itemId] = count + 1;

            int remaining = item.maxPurchase > 0 ? item.maxPurchase - (count + 1) : -1;

            EventBus.Publish(new SeasonShopPurchaseEvent
            {
                ItemId = itemId,
                RemainingPurchases = remaining
            });

            Debug.Log($"[SeasonShopSystem] 구매 완료 — {item.displayName} (남은 횟수: {remaining})");
            return true;
        }

        /// <summary>
        /// 해당 아이템의 구매 횟수를 반환한다.
        /// </summary>
        public int GetPurchaseCount(string itemId)
        {
            return _purchaseCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>
        /// 해당 아이템의 남은 구매 가능 횟수를 반환한다. -1이면 무제한.
        /// </summary>
        public int GetRemainingPurchases(string itemId)
        {
            var item = FindItem(itemId);
            if (item == null) return 0;
            if (item.maxPurchase <= 0) return -1;

            int count = GetPurchaseCount(itemId);
            return Mathf.Max(0, item.maxPurchase - count);
        }

        /// <summary>
        /// 시즌 리셋 시 구매 횟수와 시즌 포인트를 초기화한다.
        /// </summary>
        public void ResetShop()
        {
            _purchaseCounts.Clear();

            int previous = _seasonPoints;
            _seasonPoints = 0;

            EventBus.Publish(new SeasonPointChangedEvent
            {
                PreviousAmount = previous,
                CurrentAmount = 0
            });

            Debug.Log("[SeasonShopSystem] 시즌 상점 리셋 완료");
        }

        private SeasonShopItem FindItem(string itemId)
        {
            if (_items == null || string.IsNullOrEmpty(itemId)) return null;

            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] != null && _items[i].id == itemId)
                    return _items[i];
            }

            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
