using UnityEngine;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.Growth
{
    /// <summary>
    /// 헌터 등급 시스템.
    /// 능력치 포인트 25개 투자마다 등급이 1 상승하고,
    /// 등급 상승 시 ATK/HP 고정 보너스를 CombatStats에 적용한다.
    /// </summary>
    public class HunterRankSystem : MonoBehaviour
    {
        public static HunterRankSystem Instance { get; private set; }

        /// <summary>등급당 ATK 고정 보너스 (등급 1~5)</summary>
        private const int ATK_BONUS_TIER1 = 5;
        /// <summary>등급당 HP 고정 보너스 (등급 1~5)</summary>
        private const int HP_BONUS_TIER1 = 50;
        /// <summary>등급당 ATK 고정 보너스 (등급 6~10)</summary>
        private const int ATK_BONUS_TIER2 = 10;
        /// <summary>등급당 HP 고정 보너스 (등급 6~10)</summary>
        private const int HP_BONUS_TIER2 = 100;
        /// <summary>등급당 ATK 고정 보너스 (등급 11+)</summary>
        private const int ATK_BONUS_TIER3 = 15;
        /// <summary>등급당 HP 고정 보너스 (등급 11+)</summary>
        private const int HP_BONUS_TIER3 = 150;

        [SerializeField] private CombatStats _combatStats;

        private int _currentRank;

        /// <summary>현재 헌터 등급</summary>
        public int CurrentRank => _currentRank;

        /// <summary>현재 등급의 표시명</summary>
        public string CurrentRankName => GetRankName(_currentRank);

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

        private void OnEnable()
        {
            EventBus<StatAllocatedEvent>.Subscribe(OnStatAllocated);
        }

        private void OnDisable()
        {
            EventBus<StatAllocatedEvent>.Unsubscribe(OnStatAllocated);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            SyncRankFromAllocation();
        }

        /// <summary>
        /// StatAllocationSystem의 현재 HunterRank를 읽어 등급을 동기화한다.
        /// 게임 시작 시 저장 데이터 복원 후 호출.
        /// </summary>
        public void SyncRankFromAllocation()
        {
            if (StatAllocationSystem.Instance == null) return;

            int newRank = StatAllocationSystem.Instance.HunterRank;
            if (newRank == _currentRank) return;

            // 기존 보너스 제거 후 새 보너스 적용
            if (_combatStats != null && _currentRank > 0)
            {
                RemoveRankBonus(_currentRank);
            }

            _currentRank = newRank;

            if (_combatStats != null && _currentRank > 0)
            {
                ApplyRankBonus(_currentRank);
            }
        }

        /// <summary>
        /// 스탯 분배 이벤트 핸들러.
        /// 등급 변동이 있으면 보너스를 갱신한다.
        /// </summary>
        private void OnStatAllocated(StatAllocatedEvent evt)
        {
            if (StatAllocationSystem.Instance == null) return;
            if (_combatStats == null) return;

            int newRank = StatAllocationSystem.Instance.HunterRank;
            if (newRank == _currentRank) return;

            // 등급 상승 — 기존 보너스 제거 후 새 등급 보너스 적용
            if (_currentRank > 0)
            {
                RemoveRankBonus(_currentRank);
            }

            int previousRank = _currentRank;
            _currentRank = newRank;

            if (_currentRank > 0)
            {
                ApplyRankBonus(_currentRank);
            }

            Debug.Log($"[HunterRankSystem] 등급 상승! {previousRank} → {_currentRank} ({GetRankName(_currentRank)})");
        }

        /// <summary>
        /// 해당 등급까지의 누적 ATK 보너스를 계산한다.
        /// </summary>
        private int CalculateTotalAtkBonus(int rank)
        {
            int bonus = 0;

            // Tier 1: 등급 1~5
            int tier1Ranks = Mathf.Min(rank, 5);
            bonus += tier1Ranks * ATK_BONUS_TIER1;

            // Tier 2: 등급 6~10
            int tier2Ranks = Mathf.Max(0, Mathf.Min(rank, 10) - 5);
            bonus += tier2Ranks * ATK_BONUS_TIER2;

            // Tier 3: 등급 11+
            int tier3Ranks = Mathf.Max(0, rank - 10);
            bonus += tier3Ranks * ATK_BONUS_TIER3;

            return bonus;
        }

        /// <summary>
        /// 해당 등급까지의 누적 HP 보너스를 계산한다.
        /// </summary>
        private int CalculateTotalHpBonus(int rank)
        {
            int bonus = 0;

            // Tier 1: 등급 1~5
            int tier1Ranks = Mathf.Min(rank, 5);
            bonus += tier1Ranks * HP_BONUS_TIER1;

            // Tier 2: 등급 6~10
            int tier2Ranks = Mathf.Max(0, Mathf.Min(rank, 10) - 5);
            bonus += tier2Ranks * HP_BONUS_TIER2;

            // Tier 3: 등급 11+
            int tier3Ranks = Mathf.Max(0, rank - 10);
            bonus += tier3Ranks * HP_BONUS_TIER3;

            return bonus;
        }

        /// <summary>
        /// 등급에 해당하는 누적 보너스를 CombatStats에 수정자로 적용한다.
        /// </summary>
        private void ApplyRankBonus(int rank)
        {
            int atkBonus = CalculateTotalAtkBonus(rank);
            int hpBonus = CalculateTotalHpBonus(rank);
            _combatStats.SetCpReason("헌터 랭크");
            _combatStats.ClearModifiers(ModifierSource.HunterRank);
            if (atkBonus > 0)
                _combatStats.AddModifier("hunterrank_atk", new StatModifier(
                    ModifierSource.HunterRank, "hunterrank_atk", StatType.Atk, atkBonus, 0f));
            if (hpBonus > 0)
                _combatStats.AddModifier("hunterrank_hp", new StatModifier(
                    ModifierSource.HunterRank, "hunterrank_hp", StatType.MaxHp, hpBonus, 0f));
        }

        /// <summary>
        /// 등급에 해당하는 누적 보너스를 CombatStats에서 제거한다.
        /// </summary>
        private void RemoveRankBonus(int rank)
        {
            _combatStats.SetCpReason("헌터 랭크");
            _combatStats.ClearModifiers(ModifierSource.HunterRank);
        }

        /// <summary>
        /// 등급에 해당하는 표시명을 반환한다.
        /// </summary>
        public static string GetRankName(int rank)
        {
            if (rank <= 0) return "무등급";
            if (rank < 5) return "견습 헌터";
            if (rank < 10) return "숙련 헌터";
            if (rank < 15) return "베테랑 헌터";
            if (rank < 20) return "엘리트 헌터";
            if (rank < 25) return "마스터 헌터";
            return "그랜드마스터";
        }
    }
}
