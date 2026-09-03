using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.Growth
{
    /// <summary>
    /// 능력치 분배 시스템.
    /// 레벨업으로 획득한 스탯 포인트를 5가지 스탯에 투자하고,
    /// CombatStats에 보너스를 실시간 반영한다.
    /// </summary>
    public class StatAllocationSystem : MonoBehaviour
    {
        public static StatAllocationSystem Instance { get; private set; }

        /// <summary>포인트 1당 ATK 보너스</summary>
        private const int ATK_PER_POINT = 2;
        /// <summary>포인트 1당 HP 보너스</summary>
        private const int HP_PER_POINT = 10;
        /// <summary>포인트 1당 DEF 보너스</summary>
        private const int DEF_PER_POINT = 1;
        /// <summary>포인트 1당 CritRate 보너스 (0.2%)</summary>
        private const float CRIT_RATE_PER_POINT = 0.002f;
        /// <summary>포인트 1당 Accuracy 보너스 (0.3%)</summary>
        private const float ACCURACY_PER_POINT = 0.003f;
        /// <summary>헌터 등급 1단계당 필요 포인트</summary>
        private const int POINTS_PER_HUNTER_RANK = 25;

        [SerializeField] private LevelSystem _levelSystem;
        [SerializeField] private CombatStats _combatStats;

        private int _allocatedAtk;
        private int _allocatedDef;
        private int _allocatedHp;
        private int _allocatedCrit;
        private int _allocatedAccuracy;

        /// <summary>ATK에 투자한 포인트 수</summary>
        public int AllocatedAtk => _allocatedAtk;
        /// <summary>DEF에 투자한 포인트 수</summary>
        public int AllocatedDef => _allocatedDef;
        /// <summary>HP에 투자한 포인트 수</summary>
        public int AllocatedHp => _allocatedHp;
        /// <summary>CritRate에 투자한 포인트 수</summary>
        public int AllocatedCrit => _allocatedCrit;
        /// <summary>Accuracy에 투자한 포인트 수</summary>
        public int AllocatedAccuracy => _allocatedAccuracy;

        /// <summary>총 투자 포인트</summary>
        public int TotalAllocatedPoints => _allocatedAtk + _allocatedDef + _allocatedHp + _allocatedCrit + _allocatedAccuracy;

        /// <summary>
        /// 헌터 등급 (25포인트마다 1등급 상승).
        /// C3-02 헌터 등급 시스템에서 활용.
        /// </summary>
        public int HunterRank => TotalAllocatedPoints / POINTS_PER_HUNTER_RANK;

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

        private void Start()
        {
            LoadFromSave();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 지정 스탯에 포인트를 1 투자한다.
        /// </summary>
        /// <param name="statName">atk, hp, def, crit, accuracy 중 하나</param>
        /// <returns>성공 시 true</returns>
        public bool AllocatePoint(string statName)
        {
            return AllocatePoints(statName, 1);
        }

        /// <summary>
        /// 지정 스탯에 포인트를 amount만큼 투자한다.
        /// </summary>
        /// <param name="statName">atk, hp, def, crit, accuracy 중 하나</param>
        /// <param name="amount">투자할 포인트 수</param>
        /// <returns>성공 시 true</returns>
        public bool AllocatePoints(string statName, int amount)
        {
            if (amount <= 0) return false;
            if (_levelSystem == null)
            {
                Debug.LogWarning("[StatAllocationSystem] LevelSystem이 연결되지 않았습니다.");
                return false;
            }
            if (_combatStats == null)
            {
                Debug.LogWarning("[StatAllocationSystem] CombatStats가 연결되지 않았습니다.");
                return false;
            }

            if (!_levelSystem.ConsumeStatPoints(amount)) return false;

            switch (statName)
            {
                case "atk":
                    _allocatedAtk += amount;
                    break;
                case "hp":
                    _allocatedHp += amount;
                    break;
                case "def":
                    _allocatedDef += amount;
                    break;
                case "crit":
                    _allocatedCrit += amount;
                    break;
                case "accuracy":
                    _allocatedAccuracy += amount;
                    break;
                default:
                    _levelSystem.RestoreStatPoints(amount);
                    Debug.LogWarning($"[StatAllocationSystem] 알 수 없는 스탯: {statName}");
                    return false;
            }

            ApplyAllModifiers();

            SaveToData();

            int totalForStat = GetAllocatedForStat(statName);
            EventBus.Publish(new StatAllocatedEvent
            {
                StatName = statName,
                PointsSpent = amount,
                TotalAllocated = totalForStat
            });

            return true;
        }

        /// <summary>
        /// 모든 스탯 분배를 초기화하고 포인트를 회수한다.
        /// </summary>
        public void ResetAllocation()
        {
            if (_combatStats == null || _levelSystem == null) return;

            // 포인트 복원
            int totalPoints = TotalAllocatedPoints;
            _levelSystem.RestoreStatPoints(totalPoints);

            // 내부 상태 초기화
            _allocatedAtk = 0;
            _allocatedDef = 0;
            _allocatedHp = 0;
            _allocatedCrit = 0;
            _allocatedAccuracy = 0;

            // 수정자 일괄 제거
            _combatStats.SetCpReason("능력치 초기화");
            _combatStats.ClearModifiers(ModifierSource.Job);

            SaveToData();
        }

        /// <summary>
        /// 현재 분배 상태를 CombatStats 수정자로 일괄 적용한다.
        /// </summary>
        private void ApplyAllModifiers()
        {
            if (_combatStats == null) return;

            _combatStats.SetCpReason("능력치 분배");
            _combatStats.ClearModifiers(ModifierSource.Job);

            if (_allocatedAtk > 0)
                _combatStats.AddModifier("statalloc_atk", new StatModifier(
                    ModifierSource.Job, "statalloc_atk", StatType.Atk, ATK_PER_POINT * _allocatedAtk, 0f));
            if (_allocatedHp > 0)
                _combatStats.AddModifier("statalloc_hp", new StatModifier(
                    ModifierSource.Job, "statalloc_hp", StatType.MaxHp, HP_PER_POINT * _allocatedHp, 0f));
            if (_allocatedDef > 0)
                _combatStats.AddModifier("statalloc_def", new StatModifier(
                    ModifierSource.Job, "statalloc_def", StatType.Def, DEF_PER_POINT * _allocatedDef, 0f));
            if (_allocatedCrit > 0)
                _combatStats.AddModifier("statalloc_crit", new StatModifier(
                    ModifierSource.Job, "statalloc_crit", StatType.CritRate, CRIT_RATE_PER_POINT * _allocatedCrit, 0f));
        }

        /// <summary>
        /// 특정 스탯의 투자 포인트 수를 반환한다.
        /// </summary>
        public int GetAllocatedForStat(string statName)
        {
            return statName switch
            {
                "atk" => _allocatedAtk,
                "hp" => _allocatedHp,
                "def" => _allocatedDef,
                "crit" => _allocatedCrit,
                "accuracy" => _allocatedAccuracy,
                _ => 0
            };
        }

        /// <summary>
        /// 특정 스탯의 현재 보너스 값을 반환한다.
        /// </summary>
        public float GetBonusForStat(string statName)
        {
            return statName switch
            {
                "atk" => _allocatedAtk * ATK_PER_POINT,
                "hp" => _allocatedHp * HP_PER_POINT,
                "def" => _allocatedDef * DEF_PER_POINT,
                "crit" => _allocatedCrit * CRIT_RATE_PER_POINT,
                "accuracy" => _allocatedAccuracy * ACCURACY_PER_POINT,
                _ => 0f
            };
        }

        /// <summary>
        /// SaveData에서 분배 내역을 복원하고 CombatStats에 보너스를 적용한다.
        /// </summary>
        private void LoadFromSave()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var stats = SaveManager.Instance.CurrentData.player.stats;
            if (stats == null) return;

            _allocatedAtk = stats.atk;
            _allocatedDef = stats.def;
            _allocatedHp = stats.hp;
            _allocatedCrit = stats.crit;
            _allocatedAccuracy = stats.accuracy;

            // CombatStats에 수정자로 적용
            if (_combatStats == null) return;

            ApplyAllModifiers();
        }

        /// <summary>
        /// 현재 분배 상태를 SaveData에 저장한다.
        /// </summary>
        private void SaveToData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var stats = SaveManager.Instance.CurrentData.player.stats;
            if (stats == null) return;

            stats.atk = _allocatedAtk;
            stats.def = _allocatedDef;
            stats.hp = _allocatedHp;
            stats.crit = _allocatedCrit;
            stats.accuracy = _allocatedAccuracy;
        }
    }
}
