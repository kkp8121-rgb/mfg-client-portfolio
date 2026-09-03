using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 상점 종류.
    /// </summary>
    public enum ShopType
    {
        General,    // 일반 상점 (상시)
        Weekly,     // 주간 상점 (주간 리셋)
        Event       // 이벤트 상점 (기간 한정)
    }

    /// <summary>
    /// 상점 아이템 정의.
    /// </summary>
    [Serializable]
    public class ShopItem
    {
        public string id;
        public string displayName;
        public CurrencyType costType;
        public int costAmount;
        public CurrencyType rewardType;
        public int rewardAmount;
        /// <summary>최대 구매 횟수 (0 = 무제한)</summary>
        public int maxPurchaseCount;
        public ShopType shopType;
        // costumeRewardId: 2026-04-20 Costume 제거 + 2026-04-23 dead field 삭제
    }

    /// <summary>
    /// 상점 아이템 구매 기록.
    /// </summary>
    [Serializable]
    public class ShopPurchaseRecord
    {
        public string itemId;
        public int purchaseCount;
    }

    /// <summary>
    /// 상점 시스템.
    /// 일반/주간/이벤트 상점의 아이템 구매 및 재고 관리를 담당한다.
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        public static ShopSystem Instance { get; private set; }

        /// <summary>상점 아이템 카탈로그 (id → data)</summary>
        private readonly Dictionary<string, ShopItem> _catalog = new();

        /// <summary>구매 기록 (id → count)</summary>
        private readonly Dictionary<string, int> _purchaseCounts = new();

        /// <summary>마지막 주간 리셋 날짜 (yyyy-MM-dd, 월요일 기준)</summary>
        private string _lastWeeklyReset;

        /// <summary>등록된 상점 아이템 목록</summary>
        private readonly List<ShopItem> _allItems = new();

        /// <summary>모든 상점 아이템 (읽기 전용)</summary>
        public IReadOnlyList<ShopItem> AllItems => _allItems;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 상점 카탈로그를 등록하고 초기화한다.
        /// </summary>
        public void Initialize(List<ShopItem> items)
        {
            if (items == null || items.Count == 0)
            {
                Debug.LogWarning("[ShopSystem] 상점 아이템 카탈로그가 비어있음");
                return;
            }

            _catalog.Clear();
            _purchaseCounts.Clear();
            _allItems.Clear();
            _lastWeeklyReset = GetMondayDate(DateTime.Now);

            for (int i = 0; i < items.Count; i++)
            {
                ShopItem item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                _catalog[item.id] = item;
                _allItems.Add(item);
                _purchaseCounts[item.id] = 0;
            }

            Debug.Log($"[ShopSystem] 초기화 완료 — 상점 아이템 {_catalog.Count}개 등록");
        }

        /// <summary>
        /// 저장된 구매 기록을 복원한다.
        /// </summary>
        public void RestoreRecords(List<ShopPurchaseRecord> records, string lastWeeklyReset = null)
        {
            if (records == null) return;

            if (!string.IsNullOrEmpty(lastWeeklyReset))
            {
                _lastWeeklyReset = lastWeeklyReset;
            }

            for (int i = 0; i < records.Count; i++)
            {
                ShopPurchaseRecord record = records[i];
                _purchaseCounts[record.itemId] = record.purchaseCount;
            }

            // 주간 리셋 확인
            CheckWeeklyReset();

            Debug.Log($"[ShopSystem] 구매 기록 복원 완료 — {records.Count}개");
        }

        /// <summary>
        /// 해당 아이템을 구매할 수 있는지 확인한다.
        /// </summary>
        public bool CanPurchase(string itemId)
        {
            if (!_catalog.TryGetValue(itemId, out ShopItem item))
            {
                return false;
            }

            // 구매 횟수 제한 확인
            if (item.maxPurchaseCount > 0)
            {
                int purchased = GetPurchaseCount(itemId);
                if (purchased >= item.maxPurchaseCount)
                {
                    return false;
                }
            }

            // 재화 충분 확인
            if (CurrencyManager.Instance == null)
            {
                return false;
            }

            return CurrencyManager.Instance.HasEnough(item.costType, item.costAmount);
        }

        /// <summary>
        /// 아이템을 구매한다. 재화 소비 + 보상 지급.
        /// </summary>
        public bool Purchase(string itemId)
        {
            if (!_catalog.TryGetValue(itemId, out ShopItem item))
            {
                Debug.LogWarning($"[ShopSystem] 상점 아이템을 찾을 수 없음: {itemId}");
                return false;
            }

            // 구매 횟수 제한 확인
            if (item.maxPurchaseCount > 0)
            {
                int purchased = GetPurchaseCount(itemId);
                if (purchased >= item.maxPurchaseCount)
                {
                    Debug.LogWarning($"[ShopSystem] 구매 한도 초과: {itemId} ({purchased}/{item.maxPurchaseCount})");
                    return false;
                }
            }

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[ShopSystem] CurrencyManager 없음");
                return false;
            }

            // 재화 소비
            if (!CurrencyManager.Instance.Spend(item.costType, item.costAmount))
            {
                Debug.LogWarning($"[ShopSystem] 재화 부족: {item.costType} {item.costAmount}");
                return false;
            }

            // 보상 지급
            if (item.rewardAmount > 0)
            {
                CurrencyManager.Instance.Add(item.rewardType, item.rewardAmount);
            }

            // 구매 횟수 기록
            if (!_purchaseCounts.ContainsKey(itemId))
            {
                _purchaseCounts[itemId] = 0;
            }
            _purchaseCounts[itemId]++;

            EventBus.Publish(new ShopPurchaseEvent
            {
                ItemId = itemId,
                CostType = item.costType,
                CostAmount = item.costAmount,
                RewardType = item.rewardType,
                RewardAmount = item.rewardAmount
            });

            Debug.Log($"[ShopSystem] 구매 완료: {item.displayName} ({item.costType} x{item.costAmount} → {item.rewardType} x{item.rewardAmount})");
            return true;
        }

        /// <summary>
        /// 주간 상점을 리셋한다. 주간 아이템의 구매 횟수를 초기화한다.
        /// </summary>
        public void ResetWeeklyShop()
        {
            foreach (var kvp in _catalog)
            {
                if (kvp.Value.shopType == ShopType.Weekly)
                {
                    _purchaseCounts[kvp.Key] = 0;
                }
            }

            _lastWeeklyReset = GetMondayDate(DateTime.Now);
            Debug.Log("[ShopSystem] 주간 상점 리셋 완료");
        }

        /// <summary>
        /// 주간 리셋이 필요한지 확인하고 실행한다.
        /// </summary>
        public void CheckWeeklyReset()
        {
            string currentMonday = GetMondayDate(DateTime.Now);
            if (string.Equals(_lastWeeklyReset, currentMonday, StringComparison.Ordinal)) return;

            ResetWeeklyShop();
        }

        /// <summary>
        /// 특정 아이템의 구매 횟수를 반환한다.
        /// </summary>
        public int GetPurchaseCount(string itemId)
        {
            return _purchaseCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>
        /// 특정 아이템의 남은 구매 가능 횟수를 반환한다. 무제한이면 -1.
        /// </summary>
        public int GetRemainingPurchases(string itemId)
        {
            if (!_catalog.TryGetValue(itemId, out ShopItem item)) return 0;
            if (item.maxPurchaseCount <= 0) return -1;

            int purchased = GetPurchaseCount(itemId);
            return Mathf.Max(0, item.maxPurchaseCount - purchased);
        }

        /// <summary>
        /// 특정 상점 타입의 아이템 목록을 반환한다.
        /// </summary>
        public List<ShopItem> GetItemsByType(ShopType shopType)
        {
            var result = new List<ShopItem>();
            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].shopType == shopType)
                {
                    result.Add(_allItems[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 상점 아이템 데이터를 반환한다.
        /// </summary>
        public ShopItem GetItem(string itemId)
        {
            return _catalog.TryGetValue(itemId, out ShopItem item) ? item : null;
        }

        /// <summary>
        /// 현재 구매 기록을 반환한다 (저장용).
        /// </summary>
        public List<ShopPurchaseRecord> GetRecordsForSave()
        {
            var records = new List<ShopPurchaseRecord>();
            foreach (var kvp in _purchaseCounts)
            {
                if (kvp.Value > 0)
                {
                    records.Add(new ShopPurchaseRecord
                    {
                        itemId = kvp.Key,
                        purchaseCount = kvp.Value
                    });
                }
            }
            return records;
        }

        /// <summary>마지막 주간 리셋 날짜 (저장용)</summary>
        public string LastWeeklyReset => _lastWeeklyReset;

        private static string GetMondayDate(DateTime date)
        {
            int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime monday = date.AddDays(-diff);
            return monday.ToString("yyyy-MM-dd");
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
