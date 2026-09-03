using UnityEngine;
using MkLike.Core;
using MkLike.Utils;
using MkLike.Economy;

namespace MkLike.Combat
{
    /// <summary>
    /// 전리품 드롭 매니저.
    /// 몬스터 사망 시 골드, 경험치, 사냥 포인트 등을 계산하여 지급한다.
    /// 일반/미니보스/챕터보스를 구분하여 드롭 테이블을 적용한다.
    /// </summary>
    public class LootManager : MonoBehaviour
    {
        public static LootManager Instance { get; private set; }

        [Header("현재 층수")]
        [SerializeField] private int _currentFloor = 1;

        [Header("일반 몬스터 추가 드롭 확률")]
        [SerializeField] private float _runeFragmentDropRate = 0.05f;
        [SerializeField] private float _starCrystalDropRate = 0.03f;
        [SerializeField] private float _rubyDropRate = 0.005f;

        public int CurrentFloor => _currentFloor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
        }

        public void SetFloor(int floor)
        {
            _currentFloor = Mathf.Max(1, floor);
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (evt.Monster == null) return;

            MonsterController controller = evt.Monster.GetComponent<MonsterController>();
            if (controller == null)
            {
                DropNormalLoot(evt.Position);
                return;
            }

            if (controller.IsElite)
            {
                // 엘리트 드롭은 EliteSummonManager가 처리
                DropNormalLoot(evt.Position);
                return;
            }

            if (controller.IsChapterBoss)
                DropChapterBossLoot(evt.Position);
            else if (controller.IsBoss)
                DropMiniBossLoot(evt.Position);
            else
                DropNormalLoot(evt.Position);
        }

        private void DropNormalLoot(Vector3 position)
        {
            CurrencyManager cm = CurrencyManager.Instance;
            if (cm == null) return;

            long gold = CombatFormula.MonsterGold(_currentFloor);
            long exp = CombatFormula.MonsterExp(_currentFloor);

            // 부스터 배율 적용 (온라인 사냥)
            gold = ApplyBoosterMultiplier(gold, BoosterType.GoldBoost);
            exp = ApplyBoosterMultiplier(exp, BoosterType.ExpBoost);

            cm.Add(CurrencyType.Gold, gold);
            cm.Add(CurrencyType.HuntPoint, 1);

            EventBus.Publish(new GoldGainedEvent { Amount = gold, Position = position });
            EventBus.Publish(new ExpGainedEvent { Amount = exp });

            // 확률 드롭: 룬 조각 (5%)
            if (Random.value < _runeFragmentDropRate)
            {
                cm.Add(CurrencyType.RuneFragment, 1);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.RuneFragment,
                    Amount = 1,
                    Position = position
                });
            }

            // 확률 드롭: 별의 결정 (3%)
            if (Random.value < _starCrystalDropRate)
            {
                cm.Add(CurrencyType.StarCrystal, 1);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.StarCrystal,
                    Amount = 1,
                    Position = position
                });
            }

            // 확률 드롭: 루비 (0.5%)
            if (Random.value < _rubyDropRate)
            {
                cm.Add(CurrencyType.Ruby, 1);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.Ruby,
                    Amount = 1,
                    Position = position
                });
            }
        }

        /// <summary>
        /// 미니보스 드롭: 골드x10, EXP x10, 사냥포인트 10, 룬/별 확률 상승
        /// </summary>
        private void DropMiniBossLoot(Vector3 position)
        {
            CurrencyManager cm = CurrencyManager.Instance;
            if (cm == null) return;

            long gold = CombatFormula.BossGold(_currentFloor);
            long exp = CombatFormula.BossExp(_currentFloor);

            // 부스터 배율 적용 (온라인 사냥)
            gold = ApplyBoosterMultiplier(gold, BoosterType.GoldBoost);
            exp = ApplyBoosterMultiplier(exp, BoosterType.ExpBoost);

            cm.Add(CurrencyType.Gold, gold);
            cm.Add(CurrencyType.HuntPoint, 10);

            EventBus.Publish(new GoldGainedEvent { Amount = gold, Position = position });
            EventBus.Publish(new ExpGainedEvent { Amount = exp });

            // 미니보스 확률 드롭: 룬 조각 (50%, 2~5개)
            if (Random.value < 0.5f)
            {
                int amount = Random.Range(2, 6);
                cm.Add(CurrencyType.RuneFragment, amount);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.RuneFragment,
                    Amount = amount,
                    Position = position
                });
            }

            // 미니보스 확률 드롭: 별의 결정 (30%, 1~3개)
            if (Random.value < 0.3f)
            {
                int amount = Random.Range(1, 4);
                cm.Add(CurrencyType.StarCrystal, amount);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.StarCrystal,
                    Amount = amount,
                    Position = position
                });
            }

            // 미니보스 확률 드롭: 잠재의 수정 (10%)
            if (Random.value < 0.1f)
            {
                cm.Add(CurrencyType.PotentialStone, 1);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.PotentialStone,
                    Amount = 1,
                    Position = position
                });
            }

#if UNITY_EDITOR
            Debug.Log($"[LootManager] 미니보스 보상 — 층:{_currentFloor} | 골드:{gold}");
#endif
        }

        /// <summary>
        /// 챕터 보스 드롭: 골드x50, EXP x50, 확정 재료 + 루비
        /// </summary>
        private void DropChapterBossLoot(Vector3 position)
        {
            CurrencyManager cm = CurrencyManager.Instance;
            if (cm == null) return;

            long gold = CombatFormula.ChapterBossGold(_currentFloor);
            long exp = CombatFormula.ChapterBossExp(_currentFloor);

            // 부스터 배율 적용 (온라인 사냥)
            gold = ApplyBoosterMultiplier(gold, BoosterType.GoldBoost);
            exp = ApplyBoosterMultiplier(exp, BoosterType.ExpBoost);

            cm.Add(CurrencyType.Gold, gold);
            cm.Add(CurrencyType.HuntPoint, 30);

            EventBus.Publish(new GoldGainedEvent { Amount = gold, Position = position });
            EventBus.Publish(new ExpGainedEvent { Amount = exp });

            // 챕터 보스 확정 드롭: 룬 조각 5~10
            int runeAmount = Random.Range(5, 11);
            cm.Add(CurrencyType.RuneFragment, runeAmount);
            EventBus.Publish(new LootDroppedEvent
            {
                Type = CurrencyType.RuneFragment,
                Amount = runeAmount,
                Position = position
            });

            // 챕터 보스 확정 드롭: 별의 결정 3~8
            int starAmount = Random.Range(3, 9);
            cm.Add(CurrencyType.StarCrystal, starAmount);
            EventBus.Publish(new LootDroppedEvent
            {
                Type = CurrencyType.StarCrystal,
                Amount = starAmount,
                Position = position
            });

            // 챕터 보스 확률 드롭: 잠재의 수정 (50%, 1~2개)
            if (Random.value < 0.5f)
            {
                int amount = Random.Range(1, 3);
                cm.Add(CurrencyType.PotentialStone, amount);
                EventBus.Publish(new LootDroppedEvent
                {
                    Type = CurrencyType.PotentialStone,
                    Amount = amount,
                    Position = position
                });
            }

            // 챕터 보스 확정 드롭: 루비 30
            cm.Add(CurrencyType.Ruby, 30);
            EventBus.Publish(new LootDroppedEvent
            {
                Type = CurrencyType.Ruby,
                Amount = 30,
                Position = position
            });

#if UNITY_EDITOR
            Debug.Log($"[LootManager] 챕터 보스 보상 — 층:{_currentFloor} | 골드:{gold} | 룬:{runeAmount} | 별결정:{starAmount} | 루비:30");
#endif
        }

        /// <summary>부스터 배율을 적용한다. 비활성 시 원본 그대로 반환.</summary>
        private long ApplyBoosterMultiplier(long baseValue, BoosterType type)
        {
            var mgr = BoosterManager.Instance;
            if (mgr == null) return baseValue;

            float multiplier = mgr.GetMultiplier(type);
            if (multiplier <= 1f) return baseValue;

            return (long)(baseValue * multiplier);
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
