using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 테스트/디버그용 매니저. 인스펙터 버튼으로 재화 충전 등 테스트 기능 제공.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        public static TestManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>루비 10,000개 충전</summary>
        public void AddRuby10000()
        {
            if (CurrencyManager.Instance == null) return;
            CurrencyManager.Instance.Add(CurrencyType.Ruby, 10000);
            Debug.Log("[TestManager] 루비 +10,000");
        }

        /// <summary>골드 100,000 충전</summary>
        public void AddGold100000()
        {
            if (CurrencyManager.Instance == null) return;
            CurrencyManager.Instance.Add(CurrencyType.Gold, 100000);
            Debug.Log("[TestManager] 골드 +100,000");
        }

        /// <summary>즉시 레벨업 (ForceLevelUpEvent로 LevelSystem에 위임)</summary>
        public void ForceLevelUp()
        {
            EventBus.Publish(new ForceLevelUpEvent { Levels = 1 });
            Debug.Log("[TestManager] 레벨업 요청 +1");
        }

        /// <summary>10레벨 한번에 올리기</summary>
        public void ForceLevelUp10()
        {
            EventBus.Publish(new ForceLevelUpEvent { Levels = 10 });
            Debug.Log("[TestManager] 레벨업 요청 +10");
        }

        /// <summary>테스트 장비 3개 추가 (GachaResultEvent 발행)</summary>
        public void AddTestEquipment()
        {
            string[] testItems = { "weapon_iron_sword", "armor_leather", "helmet_iron_helm" };
            string[] testGrades = { "Rare", "Epic", "Unique" };
            for (int i = 0; i < testItems.Length; i++)
            {
                EventBus.Publish(new GachaResultEvent
                {
                    ItemId = testItems[i],
                    Grade = testGrades[i],
                    PoolName = "Equipment"
                });
            }
            Debug.Log("[TestManager] 테스트 장비 3종 추가 (Rare/Epic/Unique)");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
